using System.ComponentModel.DataAnnotations;

namespace RoomManagement.API.Contracts.Auth;

public sealed class LoginRequest
{
    [Required]
    public string UserNameOrEmail { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}
