namespace RoomManagement.Business.DTOs.Users;

public sealed class SelfRegisterTenantDto
{
    public string? UserName { get; set; }
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string CitizenId { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public string? Address { get; set; }
    public string? Email { get; set; }
    public string? Gender { get; set; }
}
