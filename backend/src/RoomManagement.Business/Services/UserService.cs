using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using RoomManagement.Business.DTOs.Users;
using RoomManagement.Business.Exceptions;
using RoomManagement.Business.Interfaces;
using RoomManagement.Business.Mappings;
using RoomManagement.Business.Validators;
using RoomManagement.Domain;
using RoomManagement.Domain.Constants;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Business.Services;

/// <summary>
/// Service triển khai các logic nghiệp vụ liên quan đến tài khoản người dùng.
/// Tuân thủ quy chuẩn Clean Architecture: Tầng Business tập trung vào các quy tắc kiểm tra,
/// tính toán, phân quyền và giao tiếp với dữ liệu thông qua IUserStore thay vì phụ thuộc DbContext.
/// </summary>
public sealed class UserService : IUserService
{
    /// <summary>Độ dài tối thiểu của mật khẩu.</summary>
    private const int MinimumPasswordLength = 6;

    private readonly IUserStore _userStore;
    private readonly IPasswordHasher<AppUser> _passwordHasher;
    private readonly ILogger<UserService> _logger;

    public UserService(
        IUserStore userStore,
        IPasswordHasher<AppUser> passwordHasher,
        ILogger<UserService> logger)
    {
        _userStore = userStore;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    // =========================================================================
    // ADMIN TẠO TÀI KHOẢN
    // =========================================================================

    /// <summary>
    /// Xử lý tạo tài khoản mới từ phía Quản trị viên (Admin) và phân quyền Landlord hoặc Tenant.
    /// </summary>
    /// <param name="dto">Dữ liệu tài khoản do Admin cung cấp.</param>
    /// <param name="cancellationToken">Token hỗ trợ hủy tác vụ bất đồng bộ.</param>
    /// <returns>Đối tượng UserDto chứa thông tin người dùng vừa được tạo.</returns>
    /// <exception cref="AppValidationException">Ném ra khi dữ liệu nhập vào vi phạm quy tắc nghiệp vụ (sai Role, mật khẩu yếu...).</exception>
    /// <exception cref="AppConflictException">Ném ra khi Tên đăng nhập hoặc Email đã tồn tại trong hệ thống.</exception>
    public async Task<UserDto> CreateUserByAdminAsync(CreateUserDto dto, CancellationToken cancellationToken = default)
    {
        // -------------------------------------------------------------------------
        // BƯỚC 1: KIỂM TRA TÍNH HỢP LỆ CỦA CÁC TRƯỜNG DỮ LIỆU CƠ BẢN
        // -------------------------------------------------------------------------
        ValidateCredentials(dto.UserName, dto.Password);

        // -------------------------------------------------------------------------
        // BƯỚC 2: RÀNG BUỘC VAI TRÒ (ROLE) - CHẶN LEO THANG ĐẶC QUYỀN
        // Admin chỉ được phép gán: "Landlord" (Chủ trọ) hoặc "Tenant" (Khách thuê).
        // -------------------------------------------------------------------------
        var trimmedRole = dto.Role?.Trim() ?? string.Empty;

        // Nếu cố tình tạo thêm Admin qua luồng này thì từ chối ngay lập tức
        if (string.Equals(trimmedRole, UserRoles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Admin cố tình tạo thêm tài khoản có quyền Admin thông qua API tạo người dùng thường.");
            throw new AppValidationException("Không được phép tạo thêm tài khoản Admin qua chức năng này. Chỉ cho phép chỉ định vai trò 'Landlord' hoặc 'Tenant'.");
        }

        // Kiểm tra xem vai trò có khớp với Landlord hoặc Tenant không
        string assignedRole;
        if (string.Equals(trimmedRole, UserRoles.Landlord, StringComparison.OrdinalIgnoreCase))
        {
            assignedRole = UserRoles.Landlord;
        }
        else if (string.Equals(trimmedRole, UserRoles.Tenant, StringComparison.OrdinalIgnoreCase))
        {
            assignedRole = UserRoles.Tenant;
        }
        else
        {
            throw new AppValidationException($"Vai trò '{dto.Role}' không hợp lệ. Vui lòng chỉ chọn một trong hai vai trò: '{UserRoles.Landlord}' (Chủ trọ) hoặc '{UserRoles.Tenant}' (Khách thuê).");
        }

        // -------------------------------------------------------------------------
        // BƯỚC 3: CHUẨN HÓA DỮ LIỆU ĐỂ PHỤC VỤ TÌM KIẾM VÀ SO SÁNH DUY NHẤT
        // -------------------------------------------------------------------------
        var normalizedUserName = Normalize(dto.UserName)!;
        var normalizedEmail = Normalize(dto.Email);

        // -------------------------------------------------------------------------
        // BƯỚC 4: KIỂM TRA TRÙNG LẶP DỮ LIỆU QUA STORE (USERNAME VÀ EMAIL)
        // -------------------------------------------------------------------------
        await EnsureUserNameAvailableAsync(dto.UserName, normalizedUserName, cancellationToken);
        await EnsureEmailAvailableAsync(dto.Email, normalizedEmail, cancellationToken);

        // -------------------------------------------------------------------------
        // BƯỚC 5: KHỞI TẠO THỰC THỂ NGƯỜI DÙNG (APPUSER)
        // Thông tin cá nhân để trống: người dùng sẽ bị yêu cầu điền ở lần đăng nhập đầu.
        // -------------------------------------------------------------------------
        var newUser = new AppUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = dto.UserName.Trim(),
            NormalizedUserName = normalizedUserName,
            Email = TrimOrNull(dto.Email),
            NormalizedEmail = normalizedEmail,
            Role = assignedRole,
            IsActive = true, // Tài khoản do Admin tạo được kích hoạt sẵn để sử dụng ngay
            ApprovalStatus = ApprovalStatuses.Approved, // Admin tạo thì coi như đã được duyệt
            CreatedAt = DateTime.UtcNow
        };

        // -------------------------------------------------------------------------
        // BƯỚC 6: BĂM MẬT KHẨU (PASSWORD HASHING) AN TOÀN TRƯỚC KHI LƯU
        // Tuyệt đối không lưu mật khẩu dạng văn bản rõ (plain text) vào cơ sở dữ liệu.
        // -------------------------------------------------------------------------
        newUser.PasswordHash = _passwordHasher.HashPassword(newUser, dto.Password);

        // -------------------------------------------------------------------------
        // BƯỚC 7: LƯU THÔNG TIN VÀO CƠ SỞ DỮ LIỆU THÔNG QUA STORE
        // -------------------------------------------------------------------------
        await _userStore.AddAsync(newUser, cancellationToken);
        await _userStore.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Admin đã tạo thành công tài khoản: {UserName} (ID: {UserId}) với vai trò '{Role}'",
            newUser.UserName, newUser.Id, newUser.Role);

        // -------------------------------------------------------------------------
        // BƯỚC 8: ÁNH XẠ THỰC THỂ SANG USERDTO TRẢ VỀ (LOẠI BỎ PASSWORDHASH)
        // -------------------------------------------------------------------------
        return newUser.ToUserDto();
    }

    // =========================================================================
    // CHỦ TRỌ TỰ ĐĂNG KÝ
    // =========================================================================

    /// <summary>
    /// Xử lý người dùng tự đăng ký làm chủ trọ.
    /// Kết quả: tài khoản có Role = Landlord nhưng ApprovalStatus = Pending và IsActive = false,
    /// nghĩa là CHƯA được quản lý khu trọ cho tới khi Admin duyệt.
    /// </summary>
    /// <exception cref="AppValidationException">Thiếu thông tin bắt buộc hoặc sai định dạng.</exception>
    /// <exception cref="AppConflictException">Trùng tên đăng nhập, email hoặc số CCCD.</exception>
    public async Task<UserDto> RegisterLandlordAsync(RegisterLandlordDto dto, CancellationToken cancellationToken = default)
    {
        // BƯỚC 1: Kiểm tra tên đăng nhập và mật khẩu
        ValidateCredentials(dto.UserName, dto.Password);

        // BƯỚC 2: Dựng thực thể người dùng từ dữ liệu đăng ký (đã cắt khoảng trắng thừa)
        var newUser = new AppUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = dto.UserName.Trim(),
            NormalizedUserName = Normalize(dto.UserName)!,
            Email = TrimOrNull(dto.Email),
            NormalizedEmail = Normalize(dto.Email),
            Role = UserRoles.Landlord,                  // Vai trò mong muốn là chủ trọ...
            ApprovalStatus = ApprovalStatuses.Pending,  // ...nhưng phải chờ Admin duyệt
            IsActive = false,                           // Chưa được kích hoạt cho tới khi được duyệt
            CreatedAt = DateTime.UtcNow,

            // Thông tin cá nhân
            FullName = TrimOrNull(dto.FullName),
            PhoneNumber = TrimOrNull(dto.PhoneNumber),
            CitizenId = TrimOrNull(dto.CitizenId),
            DateOfBirth = dto.DateOfBirth,
            Address = TrimOrNull(dto.Address)
        };

        // BƯỚC 3: Tự đăng ký thì bắt buộc điền đủ hồ sơ và đúng định dạng
        UserProfileRules.EnsureRequiredFieldsFilled(newUser);
        UserProfileRules.EnsureValidFormat(newUser);

        // BƯỚC 4: Kiểm tra trùng tên đăng nhập, email, CCCD qua Store
        await EnsureUserNameAvailableAsync(newUser.UserName, newUser.NormalizedUserName, cancellationToken);
        await EnsureEmailAvailableAsync(newUser.Email, newUser.NormalizedEmail, cancellationToken);
        await EnsureCitizenIdAvailableAsync(newUser.CitizenId!, excludeUserId: null, cancellationToken);

        // BƯỚC 5: Băm mật khẩu, không bao giờ lưu mật khẩu gốc
        newUser.PasswordHash = _passwordHasher.HashPassword(newUser, dto.Password);

        // BƯỚC 6: Lưu vào DB qua Store. Tài khoản và hồ sơ nằm chung bảng Users nên chỉ cần 1 lần lưu
        await _userStore.AddAsync(newUser, cancellationToken);
        await _userStore.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Người dùng {UserName} (ID: {UserId}) đã gửi yêu cầu đăng ký chủ trọ, đang chờ duyệt.",
            newUser.UserName, newUser.Id);

        return newUser.ToUserDto();
    }

    // =========================================================================
    // KIỂM TRA & HOÀN THIỆN HỒ SƠ
    // =========================================================================

    /// <summary>
    /// Trả về tình trạng hồ sơ: đã đủ chưa, nếu chưa thì thiếu những trường nào.
    /// </summary>
    /// <exception cref="AppNotFoundException">Không tìm thấy người dùng.</exception>
    public async Task<ProfileStatusDto> GetProfileStatusAsync(string userId, CancellationToken cancellationToken = default)
    {
        // Chỉ đọc dữ liệu nên gọi qua Store AsNoTracking để tối ưu
        var user = await _userStore.FindByIdAsNoTrackingAsync(userId, cancellationToken)
            ?? throw new AppNotFoundException($"Không tìm thấy người dùng có Id '{userId}'.");

        return BuildProfileStatus(user);
    }

    /// <summary>
    /// Cập nhật thông tin cá nhân. Chỉ ghi đè những trường người dùng có gửi lên.
    /// Sau khi cập nhật, hồ sơ bắt buộc phải đủ mọi trường cần thiết theo vai trò.
    /// </summary>
    /// <exception cref="AppNotFoundException">Không tìm thấy người dùng.</exception>
    /// <exception cref="AppValidationException">Vẫn còn thiếu trường bắt buộc hoặc sai định dạng.</exception>
    /// <exception cref="AppConflictException">Số CCCD đã thuộc về tài khoản khác.</exception>
    public async Task<ProfileStatusDto> CompleteProfileAsync(string userId, CompleteProfileDto dto, CancellationToken cancellationToken = default)
    {
        // BƯỚC 1: Lấy người dùng cần cập nhật qua Store (có theo dõi để lưu lại)
        var user = await _userStore.FindByIdAsync(userId, cancellationToken)
            ?? throw new AppNotFoundException($"Không tìm thấy người dùng có Id '{userId}'.");

        // BƯỚC 2: Gộp dữ liệu mới vào dữ liệu cũ
        user.FullName = TrimOrNull(dto.FullName) ?? user.FullName;
        user.PhoneNumber = TrimOrNull(dto.PhoneNumber) ?? user.PhoneNumber;
        user.CitizenId = TrimOrNull(dto.CitizenId) ?? user.CitizenId;
        user.DateOfBirth = dto.DateOfBirth ?? user.DateOfBirth;
        user.Address = TrimOrNull(dto.Address) ?? user.Address;

        // BƯỚC 3: Sau khi gộp, hồ sơ phải đầy đủ và đúng định dạng
        UserProfileRules.EnsureRequiredFieldsFilled(user);
        UserProfileRules.EnsureValidFormat(user);

        // BƯỚC 4: CCCD không được trùng với tài khoản KHÁC (bỏ qua chính người dùng này)
        if (!string.IsNullOrWhiteSpace(user.CitizenId))
        {
            await EnsureCitizenIdAvailableAsync(user.CitizenId, excludeUserId: user.Id, cancellationToken);
        }

        // BƯỚC 5: Yêu cầu Store lưu thay đổi
        await _userStore.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Người dùng {UserName} (ID: {UserId}) đã cập nhật hồ sơ cá nhân.", user.UserName, user.Id);

        return BuildProfileStatus(user);
    }

    // =========================================================================
    // CÁC HÀM HỖ TRỢ DÙNG CHUNG (PRIVATE)
    // =========================================================================

    /// <summary>
    /// Kiểm tra tên đăng nhập không trống và mật khẩu đủ độ dài tối thiểu.
    /// </summary>
    private static void ValidateCredentials(string userName, string password)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            throw new AppValidationException("Tên đăng nhập (UserName) không được để trống.");
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new AppValidationException("Mật khẩu không được để trống.");
        }

        if (password.Length < MinimumPasswordLength)
        {
            throw new AppValidationException($"Mật khẩu phải có độ dài tối thiểu từ {MinimumPasswordLength} ký tự trở lên để đảm bảo an toàn.");
        }
    }

    /// <summary>
    /// Ném lỗi 409 nếu tên đăng nhập đã có người dùng.
    /// </summary>
    private async Task EnsureUserNameAvailableAsync(string userName, string normalizedUserName, CancellationToken cancellationToken)
    {
        var isUserNameTaken = await _userStore.ExistsByNormalizedUserNameAsync(normalizedUserName, cancellationToken);

        if (isUserNameTaken)
        {
            _logger.LogWarning("Thao tác tạo người dùng thất bại: Tên đăng nhập '{UserName}' đã tồn tại.", userName);
            throw new AppConflictException($"Tên đăng nhập '{userName.Trim()}' đã có người sử dụng. Vui lòng chọn tên đăng nhập khác.");
        }
    }

    /// <summary>
    /// Ném lỗi 409 nếu email đã được dùng. Bỏ qua khi không nhập email.
    /// </summary>
    private async Task EnsureEmailAvailableAsync(string? email, string? normalizedEmail, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return;
        }

        var isEmailTaken = await _userStore.ExistsByNormalizedEmailAsync(normalizedEmail, cancellationToken);

        if (isEmailTaken)
        {
            _logger.LogWarning("Thao tác tạo người dùng thất bại: Email '{Email}' đã tồn tại.", email);
            throw new AppConflictException($"Địa chỉ email '{email?.Trim()}' đã được liên kết với một tài khoản khác trong hệ thống.");
        }
    }

    /// <summary>
    /// Ném lỗi 409 nếu số CCCD đã thuộc về một tài khoản khác.
    /// </summary>
    private async Task EnsureCitizenIdAvailableAsync(string citizenId, string? excludeUserId, CancellationToken cancellationToken)
    {
        var isCitizenIdTaken = await _userStore.ExistsByCitizenIdAsync(citizenId, excludeUserId, cancellationToken);

        if (isCitizenIdTaken)
        {
            _logger.LogWarning("Số CCCD '{CitizenId}' đã được sử dụng bởi tài khoản khác.", citizenId);
            throw new AppConflictException("Số CCCD này đã được đăng ký cho một tài khoản khác trong hệ thống.");
        }
    }

    /// <summary>
    /// Tạo kết quả tình trạng hồ sơ dựa trên quy tắc trong <see cref="UserProfileRules"/>.
    /// </summary>
    private static ProfileStatusDto BuildProfileStatus(AppUser user)
    {
        var missingFields = UserProfileRules.GetMissingFields(user);

        return new ProfileStatusDto
        {
            UserId = user.Id,
            IsProfileCompleted = missingFields.Count == 0,
            MissingFields = missingFields
        };
    }

    /// <summary>
    /// Chuẩn hóa chuỗi để so sánh trùng lặp: cắt khoảng trắng và viết hoa. Trả về null nếu chuỗi rỗng.
    /// </summary>
    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    /// <summary>
    /// Cắt khoảng trắng hai đầu. Trả về null nếu chuỗi rỗng (để DB lưu NULL thay vì chuỗi trống).
    /// </summary>
    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
