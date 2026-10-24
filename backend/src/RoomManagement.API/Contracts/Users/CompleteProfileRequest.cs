using System.ComponentModel.DataAnnotations;

namespace RoomManagement.API.Contracts.Users;

/// <summary>
/// Yêu cầu bổ sung / cập nhật thông tin cá nhân.
/// Các trường đều không bắt buộc gửi: trường nào bỏ trống thì giữ nguyên giá trị cũ.
/// Tuy nhiên sau khi cập nhật, hồ sơ phải đủ các trường bắt buộc theo vai trò (kiểm tra ở tầng Business).
/// </summary>
public sealed class CompleteProfileRequest
{
    [StringLength(150, ErrorMessage = "Họ và tên không được vượt quá 150 ký tự.")]
    public string? FullName { get; init; }

    public string? PhoneNumber { get; init; }

    public string? CitizenId { get; init; }

    /// <summary>Ngày sinh, định dạng JSON "yyyy-MM-dd".</summary>
    public DateOnly? DateOfBirth { get; init; }

    [StringLength(300, ErrorMessage = "Địa chỉ không được vượt quá 300 ký tự.")]
    public string? Address { get; init; }
}
