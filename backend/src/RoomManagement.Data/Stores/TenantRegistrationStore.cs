using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RoomManagement.Business.Exceptions;
using RoomManagement.Business.Interfaces;
using RoomManagement.Domain;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Data.Stores;

public sealed class TenantRegistrationStore : ITenantRegistrationStore
{
    private readonly RoomManagementDbContext db;
    public TenantRegistrationStore(RoomManagementDbContext db) => this.db = db;

    public Task<AppUser?> FindLandlordAsync(string code, CancellationToken ct = default) =>
        db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Role == UserRoles.Landlord && u.IsActive && u.LandlordCode == code, ct);

    public async Task<IReadOnlyList<TenantProfile>> FindByPhoneAsync(string landlordId, string phone, CancellationToken ct = default) =>
        await db.TenantProfiles.AsNoTracking().Where(p => p.OwnerLandlordUserId == landlordId && p.PhoneNumber == phone).Take(2).ToListAsync(ct);

    public Task<bool> UserNameExistsAsync(string login, CancellationToken ct = default) =>
        db.Users.AnyAsync(u => u.NormalizedUserName == login || u.NormalizedEmail == login, ct);

    public Task<TenantPhoneChallenge?> FindChallengeAsync(Guid id, CancellationToken ct = default) =>
        db.TenantPhoneChallenges.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<DateTime?> LatestChallengeAsync(string landlordId, string phone, CancellationToken ct = default) =>
        db.TenantPhoneChallenges.Where(c => c.LandlordId == landlordId && c.PhoneNumber == phone)
            .OrderByDescending(c => c.CreatedAt).Select(c => (DateTime?)c.CreatedAt).FirstOrDefaultAsync(ct);

    public Task AddChallengeAsync(TenantPhoneChallenge challenge, CancellationToken ct = default) =>
        db.TenantPhoneChallenges.AddAsync(challenge, ct).AsTask();

    public async Task SaveAsync(CancellationToken ct = default)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            throw new AppValidationException("Mã xác minh đã được sử dụng hoặc thay đổi. Vui lòng thử lại.");
        }
    }

    public async Task RegisterAsync(string landlordId, string login, string phone, Guid challengeId,
        Func<TenantProfile, AppUser> createUser, CancellationToken ct = default)
    {
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var challenge = await db.TenantPhoneChallenges.FirstOrDefaultAsync(c => c.Id == challengeId, ct);
            if (challenge is null || challenge.LandlordId != landlordId || challenge.PhoneNumber != phone ||
                challenge.VerifiedAt is null || challenge.ExpiresAt <= DateTime.UtcNow || challenge.ConsumedAt is not null)
                throw new AppValidationException("Số điện thoại chưa được xác minh hoặc mã đã hết hạn.");
            var profiles = await db.TenantProfiles.Where(p => p.OwnerLandlordUserId == landlordId && p.PhoneNumber == phone).Take(2).ToListAsync(ct);
            if (profiles.Count != 1 || profiles[0].UserId is not null || await UserNameExistsAsync(login, ct))
                throw new AppConflictException("Hồ sơ không còn đủ điều kiện đăng ký.", "PROFILE_NOT_ELIGIBLE");
            var user = createUser(profiles[0]);
            db.Users.Add(user);
            profiles[0].UserId = user.Id;
            challenge.ConsumedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 or 1205 })
        {
            throw new AppConflictException("Hồ sơ vừa được đăng ký. Vui lòng thử đăng nhập.", "ALREADY_REGISTERED");
        }
        catch (SqlException ex) when (ex.Number == 1205)
        {
            throw new AppConflictException("Hồ sơ vừa được đăng ký. Vui lòng thử đăng nhập.", "ALREADY_REGISTERED");
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new AppConflictException("Mã xác minh vừa được sử dụng. Vui lòng thử lại.", "OTP_ALREADY_USED");
        }
    }

    public async Task SelfRegisterAsync(AppUser user, TenantProfile profile, CancellationToken ct = default)
    {
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            var conflict = await db.Users.AsNoTracking().AnyAsync(u =>
                u.NormalizedUserName == user.NormalizedUserName ||
                (user.NormalizedEmail != null && u.NormalizedEmail == user.NormalizedEmail) ||
                (u.DeletedAt == null && u.PhoneNumber == user.PhoneNumber) ||
                u.CitizenId == user.CitizenId, ct);

            if (conflict)
            {
                throw new AppConflictException(
                    "Không thể đăng ký với thông tin đã nhập. Nếu bạn đã có tài khoản, vui lòng đăng nhập.",
                    "TENANT_REGISTRATION_CONFLICT");
            }

            db.Users.Add(user);
            db.TenantProfiles.Add(profile);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 or 1205 })
        {
            throw new AppConflictException(
                "Không thể đăng ký với thông tin đã nhập. Nếu bạn đã có tài khoản, vui lòng đăng nhập.",
                "TENANT_REGISTRATION_CONFLICT");
        }
        catch (SqlException ex) when (ex.Number == 1205)
        {
            throw new AppConflictException(
                "Không thể đăng ký với thông tin đã nhập. Nếu bạn đã có tài khoản, vui lòng đăng nhập.",
                "TENANT_REGISTRATION_CONFLICT");
        }
    }
}
