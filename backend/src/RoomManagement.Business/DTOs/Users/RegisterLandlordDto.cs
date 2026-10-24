namespace RoomManagement.Business.DTOs.Users;

/// <summary>
/// DTO chứa thông tin người dùng tự đăng ký làm chủ trọ.
/// Gồm 2 phần: thông tin tài khoản và thông tin cá nhân (bắt buộc điền đủ).
/// </summary>
public sealed class RegisterLandlordDto
{
    // ----- Thông tin tài khoản -----

    /// <summary>Tên đăng nhập (duy nhất trong hệ thống).</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>Email liên hệ (duy nhất trong hệ thống).</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Mật khẩu dạng chữ thường, sẽ được băm (hash) trước khi lưu DB.</summary>
    public string Password { get; set; } = string.Empty;

    // ----- Thông tin cá nhân -----

    /// <summary>Họ và tên đầy đủ.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Số điện thoại.</summary>
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>Số Căn cước công dân.</summary>
    public string CitizenId { get; set; } = string.Empty;

    /// <summary>Ngày sinh.</summary>
    public DateOnly? DateOfBirth { get; set; }

    /// <summary>Địa chỉ thường trú.</summary>
    public string Address { get; set; } = string.Empty;
}
