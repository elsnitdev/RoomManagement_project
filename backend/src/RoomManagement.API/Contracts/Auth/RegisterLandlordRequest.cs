using System.ComponentModel.DataAnnotations;

namespace RoomManagement.API.Contracts.Auth;

/// <summary>
/// Yêu cầu tự đăng ký làm chủ trọ gửi từ Client.
/// Gồm thông tin tài khoản và thông tin cá nhân, tất cả đều bắt buộc.
/// Data Annotations giúp chặn sớm dữ liệu sai ngay tại tầng API (trả về 400 tự động).
/// </summary>
public sealed class RegisterLandlordRequest
{
    // ----- Thông tin tài khoản -----

    [Required(ErrorMessage = "Tên đăng nhập không được để trống.")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Tên đăng nhập phải có độ dài từ 3 đến 50 ký tự.")]
    public string UserName { get; init; } = string.Empty;

    [Required(ErrorMessage = "Email không được để trống.")]
    [EmailAddress(ErrorMessage = "Địa chỉ email không đúng định dạng hợp lệ.")]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu không được để trống.")]
    [MinLength(6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự.")]
    public string Password { get; init; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập lại mật khẩu.")]
    [Compare(nameof(Password), ErrorMessage = "Mật khẩu nhập lại không khớp.")]
    public string ConfirmPassword { get; init; } = string.Empty;

    // ----- Thông tin cá nhân (bắt buộc với chủ trọ tự đăng ký) -----

    [Required(ErrorMessage = "Họ và tên không được để trống.")]
    [StringLength(150, ErrorMessage = "Họ và tên không được vượt quá 150 ký tự.")]
    public string FullName { get; init; } = string.Empty;

    [Required(ErrorMessage = "Số điện thoại không được để trống.")]
    public string PhoneNumber { get; init; } = string.Empty;

    [Required(ErrorMessage = "Số CCCD không được để trống.")]
    public string CitizenId { get; init; } = string.Empty;

    /// <summary>Ngày sinh, định dạng JSON "yyyy-MM-dd" (ví dụ: "1990-05-20").</summary>
    [Required(ErrorMessage = "Ngày sinh không được để trống.")]
    public DateOnly? DateOfBirth { get; init; }

    [Required(ErrorMessage = "Địa chỉ thường trú không được để trống.")]
    [StringLength(300, ErrorMessage = "Địa chỉ không được vượt quá 300 ký tự.")]
    public string Address { get; init; } = string.Empty;
}
