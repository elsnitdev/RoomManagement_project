namespace RoomManagement.Business.DTOs.Users;

/// <summary>
/// DTO chứa thông tin do Admin gửi xuống để tạo tài khoản người dùng mới.
/// Được tầng API chuyển tiếp sang tầng Business.
/// </summary>
public sealed class CreateUserDto
{
    /// <summary>
    /// Tên đăng nhập của người dùng mới (duy nhất trong hệ thống).
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Email liên hệ của người dùng (duy nhất trong hệ thống).
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Mật khẩu ban đầu được Admin cấp. Sẽ được băm (hash) trước khi lưu DB.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Vai trò chỉ định cho người dùng.
    /// Theo yêu cầu nghiệp vụ, chỉ chấp nhận: "Landlord" (Chủ trọ) hoặc "Tenant" (Khách thuê).
    /// </summary>
    public string Role { get; set; } = string.Empty;
}
