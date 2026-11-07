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
    [Required]
    public string PhoneNumber { get; init; } = string.Empty;
    [Required]
    public string LandlordCode { get; init; } = string.Empty;
    [Required]
    public Guid ChallengeId { get; init; }
    [Required]
    public string Password { get; init; } = string.Empty;
    [Required]
    public string ConfirmPassword { get; init; } = string.Empty;
}
