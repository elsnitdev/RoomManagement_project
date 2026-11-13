namespace RoomManagement.Business.DTOs.Users;

public sealed class AdminUpdateUserDto
{
    public string? FullName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? CitizenId { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Address { get; set; }
    public string? IdentityVerificationStatus { get; set; }
}
