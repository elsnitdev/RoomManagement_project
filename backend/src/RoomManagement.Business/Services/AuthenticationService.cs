using Microsoft.AspNetCore.Identity;
using RoomManagement.Business.DTOs.Users;
using RoomManagement.Business.Errors;
using RoomManagement.Business.Interfaces;
using RoomManagement.Business.Mappings;
using RoomManagement.Domain;
using RoomManagement.Domain.Constants;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Business.Services;

public sealed class AuthenticationService : IAuthenticationService
{
    private const int MinimumPasswordLength = 6;
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
        if (user is null || !await CanSignInAsync(user, cancellationToken))
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
        return user is not null && await CanSignInAsync(user, cancellationToken) ? user.ToUserDto() : null;
    }

    public async Task<UserDto> ChangePasswordAsync(string userId, string currentPassword, string newPassword, string confirmPassword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new BusinessException(BusinessErrorType.Validation, "VALIDATION_ERROR", "Người dùng không hợp lệ.");

        if (string.IsNullOrEmpty(currentPassword))
            throw new BusinessException(BusinessErrorType.Validation, "CURRENT_PASSWORD_REQUIRED", "Vui lòng nhập mật khẩu hiện tại.");

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < MinimumPasswordLength)
            throw new BusinessException(BusinessErrorType.Validation, "NEW_PASSWORD_TOO_SHORT", $"Mật khẩu mới phải có ít nhất {MinimumPasswordLength} ký tự.");

        if (newPassword != confirmPassword)
            throw new BusinessException(BusinessErrorType.Validation, "PASSWORD_CONFIRM_MISMATCH", "Mật khẩu xác nhận không khớp.");

        if (currentPassword == newPassword)
            throw new BusinessException(BusinessErrorType.Validation, "PASSWORD_UNCHANGED", "Mật khẩu mới phải khác mật khẩu hiện tại.");

        var user = await userStore.FindByIdAsync(userId, cancellationToken);
        if (user is null || !await CanSignInAsync(user, cancellationToken))
            throw new BusinessException(BusinessErrorType.InvalidOperation, "USER_UNAVAILABLE", "Tài khoản không tồn tại hoặc đã ngừng hoạt động.");

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword);
        if (verification == PasswordVerificationResult.Failed)
            throw new BusinessException(BusinessErrorType.Validation, "INVALID_CURRENT_PASSWORD", "Mật khẩu hiện tại không đúng.");

        user.PasswordHash = passwordHasher.HashPassword(user, newPassword);
        user.MustChangePassword = false;
        await userStore.SaveChangesAsync(cancellationToken);

        return user.ToUserDto();
    }

    private async Task<bool> CanSignInAsync(AppUser user, CancellationToken cancellationToken)
    {
        if (!user.IsActive || user.DeletedAt is not null || user.ApprovalStatus != ApprovalStatuses.Approved)
            return false;

        if (user.Role is UserRoles.Admin or UserRoles.Landlord or UserRoles.Tenant)
            return true;

        if (user.Role == UserRoles.Manager && !string.IsNullOrWhiteSpace(user.LandlordUserId))
        {
            var landlord = await userStore.FindByIdAsNoTrackingAsync(user.LandlordUserId, cancellationToken);
            return landlord is not null
                && landlord.IsActive
                && landlord.DeletedAt is null
                && landlord.ApprovalStatus == ApprovalStatuses.Approved
                && landlord.Role == UserRoles.Landlord;
        }

        return false;
    }
}
