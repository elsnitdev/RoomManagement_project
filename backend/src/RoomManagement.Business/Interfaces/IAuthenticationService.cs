using RoomManagement.Business.DTOs.Users;

namespace RoomManagement.Business.Interfaces;

public interface IAuthenticationService
{
    Task<UserDto?> AuthenticateAsync(string userNameOrEmail, string password, CancellationToken cancellationToken = default);
    Task<UserDto?> GetActiveUserAsync(string userId, CancellationToken cancellationToken = default);
    Task<UserDto> ChangePasswordAsync(string userId, string currentPassword, string newPassword, string confirmPassword, CancellationToken cancellationToken = default);
}
