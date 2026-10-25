using RoomManagement.API.Contracts.Users;

namespace RoomManagement.API.Contracts.Auth;

public sealed class LoginResponse
{
    public string Message { get; init; } = "Đăng nhập thành công.";
    public string AccessToken { get; init; } = string.Empty;
    public string TokenType { get; init; } = "Bearer";
    public DateTimeOffset ExpiresAt { get; init; }
    public UserResponse User { get; init; } = new();
}
