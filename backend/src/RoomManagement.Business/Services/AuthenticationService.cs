using Microsoft.AspNetCore.Identity;
using RoomManagement.Business.DTOs.Users;
using RoomManagement.Business.Interfaces;
using RoomManagement.Business.Mappings;
using RoomManagement.Domain;
using RoomManagement.Domain.Constants;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Business.Services;

public sealed class AuthenticationService : IAuthenticationService
{
    private readonly IUserStore userStore;
    private readonly IPasswordHasher<AppUser> passwordHasher;

    public AuthenticationService(IUserStore userStore, IPasswordHasher<AppUser> passwordHasher)
    {
        this.userStore = userStore;
        this.passwordHasher = passwordHasher;
    }

    public async Task<UserDto?> AuthenticateAsync(string userNameOrEmail, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userNameOrEmail) || string.IsNullOrEmpty(password))
            return null;

        var user = await userStore.FindByLoginAsync(userNameOrEmail.Trim().ToUpperInvariant(), cancellationToken);
        if (user is null || !CanSignIn(user))
            return null;

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verification == PasswordVerificationResult.Failed)
            return null;

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, password);
            await userStore.SaveChangesAsync(cancellationToken);
        }

        return user.ToUserDto();
    }

    public async Task<UserDto?> GetActiveUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await userStore.FindByIdAsNoTrackingAsync(userId, cancellationToken);
        return user is not null && CanSignIn(user) ? user.ToUserDto() : null;
    }

    private static bool CanSignIn(AppUser user) => user.IsActive &&
        user.ApprovalStatus == ApprovalStatuses.Approved &&
        user.Role is UserRoles.Admin or UserRoles.Landlord or UserRoles.Tenant;
}
