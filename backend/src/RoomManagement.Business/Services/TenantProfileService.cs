using Microsoft.Extensions.Logging;
using RoomManagement.Business.DTOs.Tenants;
using RoomManagement.Business.Exceptions;
using RoomManagement.Business.Interfaces;
using RoomManagement.Business.Mappings;
using RoomManagement.Business.Validators;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Business.Services;

/// <summary>
/// Hiện thực hóa nghiệp vụ quản lý hồ sơ người thuê dành cho Chủ trọ.
/// Tuân thủ Clean Architecture: chỉ giao tiếp qua ITenantProfileStore, không phụ thuộc vào DbContext hay EF Core.
/// </summary>
public sealed class TenantProfileService : ITenantProfileService
{
    private readonly ITenantProfileStore _store;
    private readonly ILogger<TenantProfileService> _logger;

    public TenantProfileService(
        ITenantProfileStore store,
        ILogger<TenantProfileService> logger)
    {
        _store = store;
        _logger = logger;
    }

    /// <summary>
    /// Chủ trọ tạo mới hồ sơ người thuê.
    /// </summary>
    public async Task<TenantProfileDto> CreateProfileAsync(
        string landlordId, CreateTenantProfileDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(landlordId))
        {
            throw new AppValidationException("Mã chủ trọ không hợp lệ.");
        }

        // 1. Kiểm tra tính hợp lệ của dữ liệu đầu vào
        TenantProfileRules.Validate(dto);

        var trimmedCitizenId = string.IsNullOrWhiteSpace(dto.CitizenId) ? null : dto.CitizenId.Trim();

        // 2. Chống trùng số CCCD trên toàn hệ thống
        if (trimmedCitizenId != null)
        {
            var isDuplicate = await _store.ExistsByCitizenIdAsync(trimmedCitizenId, null, ct);
            if (isDuplicate)
            {
                throw new AppConflictException(
                    "Số CCCD/CMND đã được sử dụng bởi hồ sơ người thuê khác.",
                    "CITIZEN_ID_CONFLICT");
            }
        }

        // 3. Khởi tạo thực thể TenantProfile độc lập với AppUser
        var profile = new TenantProfile
        {
            Id = Guid.NewGuid(),
            UserId = null, // Chưa có tài khoản đăng nhập
            CreatedByUserId = landlordId,
            FullName = dto.FullName.Trim(),
            PhoneNumber = dto.PhoneNumber.Trim(),
            Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim(),
            CitizenId = trimmedCitizenId,
            DateOfBirth = dto.DateOfBirth,
            Gender = string.IsNullOrWhiteSpace(dto.Gender) ? null : dto.Gender.Trim(),
            PermanentAddress = string.IsNullOrWhiteSpace(dto.PermanentAddress) ? null : dto.PermanentAddress.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = null
        };

        // 4. Lưu hồ sơ qua store
        await _store.AddAsync(profile, ct);
        await _store.SaveChangesAsync(ct);

        _logger.LogInformation("Chủ trọ '{LandlordId}' đã tạo thành công hồ sơ người thuê '{FullName}' (ProfileId: {ProfileId}).",
            landlordId, profile.FullName, profile.Id);

        return profile.ToTenantProfileDto();
    }

    /// <summary>
    /// Xem chi tiết thông tin hồ sơ người thuê trong phạm vi quản lý của chủ trọ.
    /// </summary>
    public async Task<TenantProfileDto> GetProfileByIdAsync(
        string landlordId, Guid id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(landlordId))
        {
            throw new AppValidationException("Mã chủ trọ không hợp lệ.");
        }

        // 1. Kiểm tra quyền truy cập của chủ trọ đối với hồ sơ này
        var hasAccess = await _store.HasAccessPermissionAsync(landlordId, id, ct);
        if (!hasAccess)
        {
            throw new AppNotFoundException(
                $"Không tìm thấy hồ sơ người thuê với mã '{id}' hoặc bạn không có quyền truy cập.",
                "TENANT_PROFILE_NOT_FOUND");
        }

        // 2. Lấy thông tin hồ sơ
        var profile = await _store.FindByIdAsNoTrackingAsync(id, ct)
            ?? throw new AppNotFoundException(
                $"Không tìm thấy hồ sơ người thuê với mã '{id}'.",
                "TENANT_PROFILE_NOT_FOUND");

        return profile.ToTenantProfileDto();
    }

    /// <summary>
    /// Xem danh sách hồ sơ người thuê phân trang & tìm kiếm theo từ khóa.
    /// </summary>
    public async Task<(IReadOnlyList<TenantProfileDto> Items, int TotalCount)> GetProfilesAsync(
        string landlordId, TenantProfileQueryDto query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(landlordId))
        {
            throw new AppValidationException("Mã chủ trọ không hợp lệ.");
        }

        var validPageIndex = query.PageIndex > 0 ? query.PageIndex : 1;
        var validPageSize = query.PageSize > 0 ? query.PageSize : 10;

        var (items, totalCount) = await _store.GetByLandlordAsync(
            landlordId, query.Keyword, validPageIndex, validPageSize, ct);

        var dtos = items.Select(p => p.ToTenantProfileDto()).ToList();
        return (dtos, totalCount);
    }

    /// <summary>
    /// Cập nhật thông tin hồ sơ người thuê thuộc quyền quản lý của chủ trọ.
    /// </summary>
    public async Task<TenantProfileDto> UpdateProfileAsync(
        string landlordId, Guid id, UpdateTenantProfileDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(landlordId))
        {
            throw new AppValidationException("Mã chủ trọ không hợp lệ.");
        }

        // 1. Kiểm tra quyền sở hữu / quyền truy cập
        var hasAccess = await _store.HasAccessPermissionAsync(landlordId, id, ct);
        if (!hasAccess)
        {
            throw new AppNotFoundException(
                $"Không tìm thấy hồ sơ người thuê với mã '{id}' hoặc bạn không có quyền truy cập.",
                "TENANT_PROFILE_NOT_FOUND");
        }

        // 2. Kiểm tra tính hợp lệ dữ liệu cập nhật
        TenantProfileRules.Validate(dto);

        var trimmedCitizenId = string.IsNullOrWhiteSpace(dto.CitizenId) ? null : dto.CitizenId.Trim();

        // 3. Chống trùng số CCCD với các hồ sơ khác
        if (trimmedCitizenId != null)
        {
            var isDuplicate = await _store.ExistsByCitizenIdAsync(trimmedCitizenId, id, ct);
            if (isDuplicate)
            {
                throw new AppConflictException(
                    "Số CCCD/CMND đã được sử dụng bởi hồ sơ người thuê khác.",
                    "CITIZEN_ID_CONFLICT");
            }
        }

        // 4. Tìm bản ghi có tracking để cập nhật
        var profile = await _store.FindByIdAsync(id, ct)
            ?? throw new AppNotFoundException(
                $"Không tìm thấy hồ sơ người thuê với mã '{id}'.",
                "TENANT_PROFILE_NOT_FOUND");

        // 5. Cập nhật các trường thông tin
        profile.FullName = dto.FullName.Trim();
        profile.PhoneNumber = dto.PhoneNumber.Trim();
        profile.Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim();
        profile.CitizenId = trimmedCitizenId;
        profile.DateOfBirth = dto.DateOfBirth;
        profile.Gender = string.IsNullOrWhiteSpace(dto.Gender) ? null : dto.Gender.Trim();
        profile.PermanentAddress = string.IsNullOrWhiteSpace(dto.PermanentAddress) ? null : dto.PermanentAddress.Trim();
        profile.UpdatedAt = DateTime.UtcNow;

        // 6. Lưu vào cơ sở dữ liệu
        await _store.SaveChangesAsync(ct);

        _logger.LogInformation("Chủ trọ '{LandlordId}' đã cập nhật thành công hồ sơ người thuê ID: {ProfileId}.",
            landlordId, profile.Id);

        return profile.ToTenantProfileDto();
    }
}
