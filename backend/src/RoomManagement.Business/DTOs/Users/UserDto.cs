namespace RoomManagement.Business.DTOs.Users;

/// <summary>
/// DTO chứa thông tin trả về sau khi tạo người dùng thành công.
/// Tuyệt đối KHÔNG chứa mật khẩu hoặc chuỗi băm PasswordHash để bảo mật.
/// </summary>
public sealed class UserDto
{
    /// <summary>
    /// Định danh duy nhất (GUID string) của người dùng.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Tên đăng nhập của người dùng.
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Email của người dùng.
    /// </summary>
    public string? Email { get; set; }
    public string? LandlordCode { get; set; }

    /// <summary>
    /// Vai trò của người dùng trong hệ thống (Landlord hoặc Tenant).
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Trạng thái hoạt động của tài khoản (mặc định là true khi vừa tạo).
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Trạng thái xét duyệt của tài khoản: Pending / Approved / Rejected.
    /// </summary>
    public string ApprovalStatus { get; set; } = string.Empty;

    /// <summary>
    /// Thời điểm tài khoản được khởi tạo (giờ chuẩn UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Bắt buộc đổi mật khẩu tạm ở lần đăng nhập đầu.</summary>
    public bool MustChangePassword { get; set; }

    /// <summary>ID Chủ trọ sở hữu (chỉ có giá trị khi Role là Manager).</summary>
    public string? LandlordUserId { get; set; }

    /// <summary>Trạng thái xác minh danh tính SĐT/CCCD (Unverified / Pending / Verified).</summary>
    public string IdentityVerificationStatus { get; set; } = string.Empty;

    /// <summary>Thời điểm xác minh danh tính (nếu đã xác minh).</summary>
    public DateTime? IdentityVerifiedAt { get; set; }

    public string? FullName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? CitizenId { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Address { get; set; }
    public DateTime? DeletedAt { get; set; }
}
