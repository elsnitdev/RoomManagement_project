namespace RoomManagement.API.Contracts.Users;

/// <summary>
/// Dữ liệu phản hồi gửi về cho Client sau khi tài khoản được tạo thành công.
/// Không bao gồm mật khẩu hay các thông tin bảo mật nhạy cảm.
/// </summary>
public sealed class UserResponse
{
    /// <summary>
    /// ID định danh duy nhất của người dùng.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Tên đăng nhập.
    /// </summary>
    public string UserName { get; init; } = string.Empty;

    /// <summary>
    /// Email của người dùng.
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// Vai trò đã được cấp (Landlord hoặc Tenant).
    /// </summary>
    public string Role { get; init; } = string.Empty;

    /// <summary>
    /// Trạng thái hoạt động của tài khoản.
    /// </summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// Ngày giờ tạo tài khoản (UTC).
    /// </summary>
    public DateTime CreatedAt { get; init; }
}
