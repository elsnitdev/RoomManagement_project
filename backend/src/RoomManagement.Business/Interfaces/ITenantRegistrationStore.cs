using RoomManagement.Domain.Entities;

namespace RoomManagement.Business.Interfaces;

public interface ITenantRegistrationStore
{
    Task<AppUser?> FindLandlordAsync(string code, CancellationToken ct = default);
    Task<IReadOnlyList<TenantProfile>> FindByPhoneAsync(string landlordId, string phone, CancellationToken ct = default);
    Task<bool> UserNameExistsAsync(string login, CancellationToken ct = default);
    Task<TenantPhoneChallenge?> FindChallengeAsync(Guid id, CancellationToken ct = default);
    Task<DateTime?> LatestChallengeAsync(string landlordId, string phone, CancellationToken ct = default);
    Task AddChallengeAsync(TenantPhoneChallenge challenge, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
    Task RegisterAsync(string landlordId, string login, string phone, Guid challengeId,
        Func<TenantProfile, AppUser> createUser, CancellationToken ct = default);
    Task SelfRegisterAsync(AppUser user, TenantProfile profile, CancellationToken ct = default)
        => throw new NotSupportedException();
}
