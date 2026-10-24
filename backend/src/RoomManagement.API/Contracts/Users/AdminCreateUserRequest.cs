using System.ComponentModel.DataAnnotations;

namespace RoomManagement.API.Contracts.Users;

/// <summary>
/// Yêu cầu gửi từ phía Client của Admin để tạo tài khoản người dùng mới.
/// Áp dụng Data Annotations để tự động xác thực các trường bắt buộc ngay tại tầng API.
/// </summary>
public sealed class AdminCreateUserRequest
{
    /// <summary>
    /// Tên đăng nhập (bắt buộc, từ 3 đến 50 ký tự).
    /// </summary>
    [Required(ErrorMessage = "Tên đăng nhập không được để trống.")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Tên đăng nhập phải có độ dài từ 3 đến 50 ký tự.")]
    public string UserName { get; init; } = string.Empty;

    /// <summary>
    /// Địa chỉ email người dùng (bắt buộc, đúng định dạng email).
    /// </summary>
    [Required(ErrorMessage = "Email không được để trống.")]
    [EmailAddress(ErrorMessage = "Địa chỉ email không đúng định dạng hợp lệ.")]
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// Mật khẩu ban đầu được Admin cấp (bắt buộc, tối thiểu 6 ký tự).
    /// </summary>
    [Required(ErrorMessage = "Mật khẩu không được để trống.")]
    [MinLength(6, ErrorMessage = "Mật khẩu ban đầu phải có ít nhất 6 ký tự.")]
    public string Password { get; init; } = string.Empty;

    /// <summary>
    /// Vai trò chỉ định cho người dùng: chỉ chấp nhận "Landlord" hoặc "Tenant".
    /// </summary>
    [Required(ErrorMessage = "Vui lòng chỉ định vai trò cho người dùng (Landlord hoặc Tenant).")]
    public string Role { get; init; } = string.Empty;
}
