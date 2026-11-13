using Microsoft.Extensions.Logging;
using RoomManagement.Business.DTOs;
using RoomManagement.Business.DTOs.Tenants;
using RoomManagement.Business.Errors;
using RoomManagement.Business.Exceptions;
using RoomManagement.Business.Interfaces;
using RoomManagement.Business.Mappings;
using RoomManagement.Business.Validators;
using RoomManagement.Domain;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

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
    private readonly ILandlordStore? _landlordStore;
    private readonly TenantLookupRateLimiter? _rateLimiter;

    public TenantProfileService(
        ITenantProfileStore store,
        ILogger<TenantProfileService> logger,
        ILandlordStore? landlordStore = null,
        TenantLookupRateLimiter? rateLimiter = null)
    {
        _store = store;
        _logger = logger;
        _landlordStore = landlordStore;
        _rateLimiter = rateLimiter;
    }

    private async Task<OperatorContext> ResolveOperatorAsync(string actorOrLandlordId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(actorOrLandlordId))
            throw new AppValidationException("Mã chủ trọ không hợp lệ.");

        if (_landlordStore is null)
            return new OperatorContext(actorOrLandlordId, actorOrLandlordId, UserRoles.Landlord, IsManager: false);

        var context = await _landlordStore.GetOperatorContextAsync(actorOrLandlordId, ct);
        if (context is null)
            throw new BusinessException(BusinessErrorType.InvalidOperation, "LANDLORD_UNAVAILABLE",
                "Tài khoản hiện tại không tồn tại, đã bị khóa hoặc chưa đổi mật khẩu tạm.");
        return context;
    }

    /// <summary>
    /// Nghiệp vụ: Chủ trọ tạo mới hồ sơ người thuê (không cần tạo tài khoản AppUser).
    /// </summary>
    public async Task<TenantProfileDto> CreateProfileAsync(
        string landlordId, CreateTenantProfileDto dto, CancellationToken ct = default)
    {
        var context = await ResolveOperatorAsync(landlordId, ct);
        var ownerLandlordId = context.OwnerLandlordUserId;

        // Bước 1: Kiểm tra tính hợp lệ của dữ liệu đầu vào (Họ tên, SĐT, CCCD, Ngày sinh, Giới tính...)
        TenantProfileRules.Validate(dto);

        // Chuẩn hóa số CCCD: nếu rỗng hoặc chỉ có khoảng trắng thì chuyển về null, ngược lại cắt khoảng trắng 2 đầu
        var trimmedCitizenId = string.IsNullOrWhiteSpace(dto.CitizenId) ? null : dto.CitizenId.Trim();

        // Bước 2: Chống trùng số CCCD trong phạm vi Landlord sở hữu (nếu người dùng có nhập CCCD)
        if (trimmedCitizenId != null)
        {
            // Gọi xuống Store kiểm tra xem đã có hồ sơ nào mang số CCCD này chưa
            var isDuplicate = await _store.ExistsByCitizenIdAsync(ownerLandlordId, trimmedCitizenId, null, ct);
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
            OwnerLandlordUserId = ownerLandlordId, // Chủ trọ sở hữu hồ sơ
            CreatedByUserId = context.ActorUserId, // Ghi nhận actor thực sự tạo hồ sơ để audit
            FullName = dto.FullName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(dto.PhoneNumber) ? null : dto.PhoneNumber.Trim(),
            // Toán tử 3 ngôi (điều_kiện ? giá_trị_đúng : giá_trị_sai): chuẩn hóa các chuỗi tùy chọn rỗng về null
            Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim(),
            CitizenId = trimmedCitizenId,
            DateOfBirth = dto.DateOfBirth,
            Gender = ParseGender(dto.Gender),
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
        var context = await ResolveOperatorAsync(landlordId, ct);

        // Bước 1: Kiểm tra quyền truy cập đối với hồ sơ qua hợp đồng/Property được phép truy cập
        var hasAccess = await _store.HasContractAccessPermissionAsync(context, id, ct);
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
        var context = await ResolveOperatorAsync(landlordId, ct);

        // Đảm bảo chỉ số trang và kích thước trang luôn hợp lệ (> 0)
        var validPageIndex = query.PageIndex > 0 ? query.PageIndex : 1;
        var validPageSize = query.PageSize > 0 ? query.PageSize : 10;

        // Gọi xuống Store để lấy danh sách phân trang và tổng số lượng bản ghi
        // Cú pháp Deconstructing Tuple: tách kết quả trả về thành 2 biến 'items' và 'totalCount'
        var (items, totalCount) = await _store.GetByContractAccessAsync(
            context, query.Keyword, validPageIndex, validPageSize, ct);

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
        var context = await ResolveOperatorAsync(landlordId, ct);
        var ownerLandlordId = context.OwnerLandlordUserId;

        // Bước 1: Kiểm tra quyền sở hữu / quyền truy cập qua hợp đồng/Property trước khi cho phép sửa
        var hasAccess = await _store.HasContractAccessPermissionAsync(context, id, ct);
        if (!hasAccess)
        {
            throw new AppNotFoundException(
                $"Không tìm thấy hồ sơ người thuê với mã '{id}' hoặc bạn không có quyền truy cập.",
                "TENANT_PROFILE_NOT_FOUND");
        }

        // Bước 2: Kiểm tra tính hợp lệ của dữ liệu cập nhật mới
        TenantProfileRules.Validate(dto);
        if (string.IsNullOrWhiteSpace(dto.PhoneNumber) && string.IsNullOrWhiteSpace(dto.Email) &&
            await _store.IsContractContactAsync(id, ct))
            throw new AppValidationException("Người liên hệ của hợp đồng cần có số điện thoại hoặc email.");

        var trimmedCitizenId = string.IsNullOrWhiteSpace(dto.CitizenId) ? null : dto.CitizenId.Trim();

        // Bước 3: Chống trùng số CCCD với các hồ sơ khác (truyền 'id' vào tham số excludeId để loại trừ chính hồ sơ đang sửa)
        if (trimmedCitizenId != null)
        {
            var isDuplicate = await _store.ExistsByCitizenIdAsync(ownerLandlordId, trimmedCitizenId, id, ct);
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

        if (profile.OwnerLandlordUserId is null && profile.UserId is not null)
            throw new BusinessRuleException(
                "Không thể chỉnh sửa trực tiếp hồ sơ định danh do người thuê tự quản lý.",
                "TENANT_IDENTITY_READONLY");

        if (profile.UserId is not null &&
            profile.PhoneNumber != (string.IsNullOrWhiteSpace(dto.PhoneNumber) ? null : dto.PhoneNumber.Trim()))
            throw new AppValidationException("Không thể đổi số điện thoại của hồ sơ đã có tài khoản. Cần xác minh lại số mới.");

        // Bước 5: Gán các giá trị mới vào thực thể (không tự ý tạo tài khoản hay sửa chứng từ hợp đồng)
        profile.FullName = dto.FullName.Trim();
        profile.PhoneNumber = string.IsNullOrWhiteSpace(dto.PhoneNumber) ? null : dto.PhoneNumber.Trim();
        profile.Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim();
        profile.CitizenId = trimmedCitizenId;
        profile.DateOfBirth = dto.DateOfBirth;
        profile.Gender = ParseGender(dto.Gender);
        profile.PermanentAddress = string.IsNullOrWhiteSpace(dto.PermanentAddress) ? null : dto.PermanentAddress.Trim();
        profile.UpdatedAt = DateTime.UtcNow; // Ghi nhận thời điểm cập nhật gần nhất

        // Bước 6: Lưu thay đổi xuống cơ sở dữ liệu
        await _store.SaveChangesAsync(ct);

        _logger.LogInformation("Chủ trọ '{LandlordId}' đã cập nhật thành công hồ sơ người thuê ID: {ProfileId}.",
            landlordId, profile.Id);

        return profile.ToTenantProfileDto();
    }

    public async Task<TenantLookupResultDto> LookupExactAsync(
        string actorId, LookupTenantAccountInput input, CancellationToken ct = default)
    {
        var context = await ResolveOperatorAsync(actorId, ct);

        if (_rateLimiter is not null && !_rateLimiter.TryAcquire(context.ActorUserId))
            throw new BusinessException(BusinessErrorType.InvalidOperation, "LOOKUP_RATE_LIMITED",
                "Bạn đã tra cứu quá nhiều lần trong thời gian ngắn. Vui lòng thử lại sau.");

        if (input.PropertyId.HasValue && _landlordStore is not null)
        {
            var hasPropertyAccess = await _landlordStore.HasPropertyAccessAsync(
                context.ActorUserId, input.PropertyId.Value, ct);
            if (!hasPropertyAccess)
                throw new AppNotFoundException("Không tìm thấy khu trọ hoặc bạn không có quyền truy cập.", "PROPERTY_NOT_FOUND");
        }

        var normalizedPhone = TenantRegistrationService.NormalizePhone(input.PhoneNumber);
        var normalizedCitizenId = (input.CitizenId ?? string.Empty).Trim();

        if (!System.Text.RegularExpressions.Regex.IsMatch(normalizedPhone, "^0[0-9]{9}$"))
            throw new AppValidationException("Số điện thoại phải gồm 10 chữ số và bắt đầu bằng 0.");
        if (!System.Text.RegularExpressions.Regex.IsMatch(normalizedCitizenId, "^([0-9]{9}|[0-9]{12})$"))
            throw new AppValidationException("Số CCCD/CMND phải gồm 9 hoặc 12 chữ số.");

        var maskedPhone = MaskValue(normalizedPhone, 3, 3);
        var maskedCitizenId = MaskValue(normalizedCitizenId, 0, 4);

        var result = await _store.LookupExactTenantAsync(
            context, input.PropertyId, normalizedPhone, normalizedCitizenId,
            maskedPhone, maskedCitizenId, ct);

        if (result is null)
            throw new AppNotFoundException(
                "Không tìm thấy tài khoản người thuê khớp chính xác số điện thoại và số CCCD.",
                "TENANT_NOT_FOUND");

        return result;
    }

    private static string MaskValue(string value, int keepStart, int keepEnd)
    {
        if (value.Length <= keepStart + keepEnd)
            return new string('*', value.Length);
        var start = keepStart > 0 ? value[..keepStart] : string.Empty;
        var end = keepEnd > 0 ? value[^keepEnd..] : string.Empty;
        return $"{start}{new string('*', value.Length - keepStart - keepEnd)}{end}";
    }

    private static Gender? ParseGender(string? gender) =>
        string.IsNullOrWhiteSpace(gender) ? null : Enum.Parse<Gender>(gender.Trim(), ignoreCase: true);
}
