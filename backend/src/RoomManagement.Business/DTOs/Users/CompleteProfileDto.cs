namespace RoomManagement.Business.DTOs.Users;

/// <summary>
/// DTO chứa thông tin cá nhân người dùng gửi lên để hoàn thiện hồ sơ.
/// Trường nào để trống (null hoặc chỉ có khoảng trắng) sẽ giữ nguyên giá trị cũ trong DB.
/// </summary>
public sealed class CompleteProfileDto
{
    public string? FullName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? CitizenId { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Address { get; set; }
}
