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
/// Tuân thủ Clean Architecture: chỉ giao tiếp qua interface ITenantProfileStore, không phụ thuộc vào DbContext hay EF Core.
/// </summary>
public sealed class TenantProfileService : ITenantProfileService
{
    // Các dependency (phụ thuộc) được tiêm vào qua Constructor (Dependency Injection)
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
    /// Nghiệp vụ: Chủ trọ tạo mới hồ sơ người thuê (không cần tạo tài khoản AppUser).
    /// </summary>
    public async Task<TenantProfileDto> CreateProfileAsync(
        string landlordId, CreateTenantProfileDto dto, CancellationToken ct = default)
    {
        // Kiểm tra ID chủ trọ phải hợp lệ
        if (string.IsNullOrWhiteSpace(landlordId))
        {
            throw new AppValidationException("Mã chủ trọ không hợp lệ.");
        }

        // Bước 1: Kiểm tra tính hợp lệ của dữ liệu đầu vào (Họ tên, SĐT, CCCD, Ngày sinh, Giới tính...)
        TenantProfileRules.Validate(dto);

        // Chuẩn hóa số CCCD: nếu rỗng hoặc chỉ có khoảng trắng thì chuyển về null, ngược lại cắt khoảng trắng 2 đầu
        var trimmedCitizenId = string.IsNullOrWhiteSpace(dto.CitizenId) ? null : dto.CitizenId.Trim();

        // Bước 2: Chống trùng số CCCD trên toàn hệ thống (nếu người dùng có nhập CCCD)
        if (trimmedCitizenId != null)
        {
            // Gọi xuống Store kiểm tra xem đã có hồ sơ nào mang số CCCD này chưa
            var isDuplicate = await _store.ExistsByCitizenIdAsync(trimmedCitizenId, null, ct);
            if (isDuplicate)
            {
                // Ném ngoại lệ xung đột dữ liệu (sẽ trả về HTTP 409 Conflict cho Frontend)
                throw new AppConflictException(
                    "Số CCCD/CMND đã được sử dụng bởi hồ sơ người thuê khác.",
                    "CITIZEN_ID_CONFLICT");
            }
        }

        // Bước 3: Khởi tạo thực thể TenantProfile độc lập với AppUser
        var profile = new TenantProfile
        {
            Id = Guid.NewGuid(), // Tự động sinh mã định danh riêng duy nhất cho hồ sơ
            UserId = null, // Tiêu chí quan trọng: Không tạo tài khoản đăng nhập, để UserId = null
            CreatedByUserId = landlordId, // Ghi nhận chủ trọ nào đã tạo hồ sơ này để phân quyền
            FullName = dto.FullName.Trim(),
            PhoneNumber = dto.PhoneNumber.Trim(),
            // Toán tử 3 ngôi (điều_kiện ? giá_trị_đúng : giá_trị_sai): chuẩn hóa các chuỗi tùy chọn rỗng về null
            Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim(),
            CitizenId = trimmedCitizenId,
            DateOfBirth = dto.DateOfBirth,
            Gender = string.IsNullOrWhiteSpace(dto.Gender) ? null : dto.Gender.Trim(),
            PermanentAddress = string.IsNullOrWhiteSpace(dto.PermanentAddress) ? null : dto.PermanentAddress.Trim(),
            CreatedAt = DateTime.UtcNow, // Lưu thời gian tạo theo chuẩn UTC
            UpdatedAt = null
        };

        // Bước 4: Đưa hồ sơ vào Store và lưu xuống cơ sở dữ liệu
        await _store.AddAsync(profile, ct);
        await _store.SaveChangesAsync(ct);

        // Ghi nhật ký hệ thống (Log) để phục vụ theo dõi và tra cứu
        _logger.LogInformation("Chủ trọ '{LandlordId}' đã tạo thành công hồ sơ người thuê '{FullName}' (ProfileId: {ProfileId}).",
            landlordId, profile.FullName, profile.Id);

        // Chuyển đổi từ Entity sang DTO trước khi trả về cho Controller
        return profile.ToTenantProfileDto();
    }

    /// <summary>
    /// Nghiệp vụ: Xem chi tiết thông tin hồ sơ người thuê trong phạm vi quản lý của chủ trọ.
    /// </summary>
    public async Task<TenantProfileDto> GetProfileByIdAsync(
        string landlordId, Guid id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(landlordId))
        {
            throw new AppValidationException("Mã chủ trọ không hợp lệ.");
        }

        // Bước 1: Kiểm tra quyền truy cập của chủ trọ đối với hồ sơ này
        // (Chỉ cho phép xem nếu chủ trọ là người tạo HOẶC khách thuê có hợp đồng tại khu trọ của chủ trọ này)
        var hasAccess = await _store.HasAccessPermissionAsync(landlordId, id, ct);
        if (!hasAccess)
        {
            // Ném lỗi không tìm thấy / không có quyền (sẽ trả về HTTP 404 Not Found)
            throw new AppNotFoundException(
                $"Không tìm thấy hồ sơ người thuê với mã '{id}' hoặc bạn không có quyền truy cập.",
                "TENANT_PROFILE_NOT_FOUND");
        }

        // Bước 2: Lấy thông tin hồ sơ từ cơ sở dữ liệu
        // Toán tử '??' (Null-coalescing): nếu kết quả bên trái là null thì thực hiện lệnh throw bên phải
        var profile = await _store.FindByIdAsNoTrackingAsync(id, ct)
            ?? throw new AppNotFoundException(
                $"Không tìm thấy hồ sơ người thuê với mã '{id}'.",
                "TENANT_PROFILE_NOT_FOUND");

        // Chuyển đổi sang DTO và trả về
        return profile.ToTenantProfileDto();
    }

    /// <summary>
    /// Nghiệp vụ: Xem danh sách hồ sơ người thuê có phân trang & tìm kiếm theo từ khóa.
    /// </summary>
    public async Task<(IReadOnlyList<TenantProfileDto> Items, int TotalCount)> GetProfilesAsync(
        string landlordId, TenantProfileQueryDto query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(landlordId))
        {
            throw new AppValidationException("Mã chủ trọ không hợp lệ.");
        }

        // Đảm bảo chỉ số trang và kích thước trang luôn hợp lệ (> 0)
        var validPageIndex = query.PageIndex > 0 ? query.PageIndex : 1;
        var validPageSize = query.PageSize > 0 ? query.PageSize : 10;

        // Gọi xuống Store để lấy danh sách phân trang và tổng số lượng bản ghi
        // Cú pháp Deconstructing Tuple: tách kết quả trả về thành 2 biến 'items' và 'totalCount'
        var (items, totalCount) = await _store.GetByLandlordAsync(
            landlordId, query.Keyword, validPageIndex, validPageSize, ct);

        // Dùng LINQ .Select() để ánh xạ từng phần tử TenantProfile sang TenantProfileDto
        var dtos = items.Select(p => p.ToTenantProfileDto()).ToList();
        return (dtos, totalCount);
    }

    /// <summary>
    /// Nghiệp vụ: Cập nhật thông tin hồ sơ người thuê thuộc quyền quản lý của chủ trọ.
    /// </summary>
    public async Task<TenantProfileDto> UpdateProfileAsync(
        string landlordId, Guid id, UpdateTenantProfileDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(landlordId))
        {
            throw new AppValidationException("Mã chủ trọ không hợp lệ.");
        }

        // Bước 1: Kiểm tra quyền sở hữu / quyền truy cập trước khi cho phép sửa
        var hasAccess = await _store.HasAccessPermissionAsync(landlordId, id, ct);
        if (!hasAccess)
        {
            throw new AppNotFoundException(
                $"Không tìm thấy hồ sơ người thuê với mã '{id}' hoặc bạn không có quyền truy cập.",
                "TENANT_PROFILE_NOT_FOUND");
        }

        // Bước 2: Kiểm tra tính hợp lệ của dữ liệu cập nhật mới
        TenantProfileRules.Validate(dto);

        var trimmedCitizenId = string.IsNullOrWhiteSpace(dto.CitizenId) ? null : dto.CitizenId.Trim();

        // Bước 3: Chống trùng số CCCD với các hồ sơ khác (truyền 'id' vào tham số excludeId để loại trừ chính hồ sơ đang sửa)
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

        // Bước 4: Tìm bản ghi có tracking từ Store để cập nhật
        var profile = await _store.FindByIdAsync(id, ct)
            ?? throw new AppNotFoundException(
                $"Không tìm thấy hồ sơ người thuê với mã '{id}'.",
                "TENANT_PROFILE_NOT_FOUND");

        // Bước 5: Gán các giá trị mới vào thực thể (không tự ý tạo tài khoản hay sửa chứng từ hợp đồng)
        profile.FullName = dto.FullName.Trim();
        profile.PhoneNumber = dto.PhoneNumber.Trim();
        profile.Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim();
        profile.CitizenId = trimmedCitizenId;
        profile.DateOfBirth = dto.DateOfBirth;
        profile.Gender = string.IsNullOrWhiteSpace(dto.Gender) ? null : dto.Gender.Trim();
        profile.PermanentAddress = string.IsNullOrWhiteSpace(dto.PermanentAddress) ? null : dto.PermanentAddress.Trim();
        profile.UpdatedAt = DateTime.UtcNow; // Ghi nhận thời điểm cập nhật gần nhất

        // Bước 6: Lưu thay đổi xuống cơ sở dữ liệu
        await _store.SaveChangesAsync(ct);

        _logger.LogInformation("Chủ trọ '{LandlordId}' đã cập nhật thành công hồ sơ người thuê ID: {ProfileId}.",
            landlordId, profile.Id);

        return profile.ToTenantProfileDto();
    }
}
