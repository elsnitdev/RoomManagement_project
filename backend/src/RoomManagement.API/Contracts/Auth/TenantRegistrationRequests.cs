using System.ComponentModel.DataAnnotations;

namespace RoomManagement.API.Contracts.Auth;

public sealed class CheckTenantRegistrationRequest
{
    [Required]
    public string PhoneNumber { get; init; } = string.Empty;
    [Required]
    public string LandlordCode { get; init; } = string.Empty;
}

public sealed class VerifyTenantOtpRequest : CheckTenantRegistrationRequestBase
{
    [Required]
    public Guid ChallengeId { get; init; }
    [Required]
    public string Code { get; init; } = string.Empty;
}

public class CheckTenantRegistrationRequestBase
{
    [Required]
    public string PhoneNumber { get; init; } = string.Empty;
    [Required]
    public string LandlordCode { get; init; } = string.Empty;
}

public sealed class RegisterTenantRequest
{
    public string? UserName { get; init; }
    [Required]
    public string FullName { get; init; } = string.Empty;
    [Required]
    public string PhoneNumber { get; init; } = string.Empty;
    [Required]
    public string CitizenId { get; init; } = string.Empty;
    [Required]
    public DateOnly? DateOfBirth { get; init; }
    [Required]
    public string Address { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string? Gender { get; init; }
    [Required]
    public string Password { get; init; } = string.Empty;
    [Required]
    public string ConfirmPassword { get; init; } = string.Empty;
}
