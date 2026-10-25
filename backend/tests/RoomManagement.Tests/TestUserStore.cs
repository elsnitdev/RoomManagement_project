using System.Collections.Concurrent;
using Microsoft.AspNetCore.Identity;
using RoomManagement.Business.Interfaces;
using RoomManagement.Domain;
using RoomManagement.Domain.Constants;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Tests;

internal sealed class TestUserStore : IUserStore, ILandlordApprovalStore
{
    public const string Password = "OnlyForAutomatedTests!42";
    public ConcurrentDictionary<string, AppUser> Users { get; } = new();
    public ConcurrentBag<string> LookedUpIds { get; } = [];

    public AppUser AddUser(string role = UserRoles.Tenant, string status = ApprovalStatuses.Approved, bool active = true)
    {
        var user = new AppUser
        {
            Id = Guid.NewGuid().ToString(), UserName = Guid.NewGuid().ToString("N"), Role = role,
            IsActive = active, ApprovalStatus = status, CreatedAt = DateTime.UtcNow
        };
        user.NormalizedUserName = user.UserName.ToUpperInvariant();
        user.Email = user.UserName + "@example.com";
        user.NormalizedEmail = user.Email.ToUpperInvariant();
        user.PasswordHash = new PasswordHasher<AppUser>().HashPassword(user, Password);
        Users[user.Id] = user;
        return user;
    }

    public Task<AppUser?> FindByLoginAsync(string normalizedLogin, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.Values.SingleOrDefault(u => u.NormalizedUserName == normalizedLogin || u.NormalizedEmail == normalizedLogin));

    public Task<AppUser?> FindByIdAsNoTrackingAsync(string userId, CancellationToken cancellationToken = default)
    {
        LookedUpIds.Add(userId);
        return Task.FromResult(Users.GetValueOrDefault(userId));
    }

    public Task<AppUser?> FindByIdAsync(string userId, CancellationToken cancellationToken = default) => FindByIdAsNoTrackingAsync(userId, cancellationToken);
    public Task<bool> ExistsByNormalizedUserNameAsync(string normalizedUserName, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.Values.Any(u => u.NormalizedUserName == normalizedUserName));
    public Task<bool> ExistsByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.Values.Any(u => u.NormalizedEmail == normalizedEmail));
    public Task<bool> ExistsByCitizenIdAsync(string citizenId, string? excludeUserId = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.Values.Any(u => u.CitizenId == citizenId && u.Id != excludeUserId));
    public Task AddAsync(AppUser user, CancellationToken cancellationToken = default)
    {
        Users[user.Id] = user;
        return Task.CompletedTask;
    }
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<IReadOnlyList<AppUser>> GetPendingLandlordsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AppUser>>(Users.Values.Where(u => u.Role == UserRoles.Landlord && u.ApprovalStatus == ApprovalStatuses.Pending).ToList());
    public Task<AppUser?> FindLandlordByIdAsync(string userId, CancellationToken cancellationToken = default) => FindByIdAsync(userId, cancellationToken);
    public Task<bool> TryReviewPendingAsync(AppUser reviewedUser, CancellationToken cancellationToken = default) => Task.FromResult(true);
}
