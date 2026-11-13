using Microsoft.AspNetCore.Identity;
using RoomManagement.Business.Exceptions;
using RoomManagement.Business.Interfaces;
using RoomManagement.Business.Services;
using RoomManagement.Domain;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Tests;

public sealed class TenantRegistrationServiceTests
{
    private const string Code = "L000000001";
    private const string Phone = "0901234567";

    [Fact]
    public async Task Check_IsScopedToLandlord()
    {
        var store = new FakeStore();
        var service = new TenantRegistrationService(store, new PasswordHasher<AppUser>(), new FakeSms());
        Assert.Equal("profile_not_found", await service.CheckAsync(Code, Phone));
        store.Profiles.Add(new TenantProfile { FullName = "Người thuê", PhoneNumber = Phone, CreatedByUserId = "landlord-2" });
        Assert.Equal("profile_not_found", await service.CheckAsync(Code, Phone));
        store.Profiles.Add(new TenantProfile { FullName = "Người thuê", PhoneNumber = Phone, CreatedByUserId = "landlord-1" });
        Assert.Equal("eligible", await service.CheckAsync(Code, Phone));
    }

    [Fact]
    public async Task Register_RequiresVerifiedChallenge_AndLinksOnlyMatchingProfile()
    {
        var store = new FakeStore();
        var profile = new TenantProfile { FullName = "Người thuê", PhoneNumber = Phone, CreatedByUserId = "landlord-1" };
        store.Profiles.Add(profile);
        var sms = new FakeSms();
        var service = new TenantRegistrationService(store, new PasswordHasher<AppUser>(), sms);
        var challengeId = await service.RequestOtpAsync(Code, Phone);
        await Assert.ThrowsAsync<AppValidationException>(() => service.RegisterAsync(Code, Phone, challengeId, "secret123", "secret123"));
        Assert.NotNull(sms.LastCode);
        await Assert.ThrowsAsync<AppValidationException>(() => service.VerifyOtpAsync(Code, Phone, challengeId, "000000" == sms.LastCode ? "111111" : "000000"));
        await service.VerifyOtpAsync(Code, Phone, challengeId, sms.LastCode!);
        var user = await service.RegisterAsync(Code, Phone, challengeId, "secret123", "secret123");
        Assert.Equal(UserRoles.Tenant, user.Role);
        Assert.Equal($"{Code}:{Phone}", user.UserName);
        Assert.Equal(user.Id, profile.UserId);
        Assert.NotEqual("secret123", store.Created!.PasswordHash);
        await Assert.ThrowsAsync<AppValidationException>(() => service.RegisterAsync(Code, Phone, challengeId, "secret123", "secret123"));
    }

    private sealed class FakeSms : ISmsOtpSender
    {
        public string GenerateCode() => "1111";
        public string? LastCode { get; private set; }
        public Task SendAsync(string phoneNumber, string code, CancellationToken ct = default)
        {
            LastCode = code;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeStore : ITenantRegistrationStore
    {
        public List<TenantProfile> Profiles { get; } = [];
        public List<TenantPhoneChallenge> Challenges { get; } = [];
        public AppUser? Created { get; private set; }
        public Task<AppUser?> FindLandlordAsync(string code, CancellationToken ct = default) =>
            Task.FromResult<AppUser?>(code == Code ? new AppUser { Id = "landlord-1", Role = UserRoles.Landlord, LandlordCode = Code } : null);
        public Task<IReadOnlyList<TenantProfile>> FindByPhoneAsync(string landlordId, string phone, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TenantProfile>>(Profiles.Where(p => p.CreatedByUserId == landlordId && p.PhoneNumber == phone).Take(2).ToList());
        public Task<bool> UserNameExistsAsync(string login, CancellationToken ct = default) => Task.FromResult(Created?.UserName == login);
        public Task<TenantPhoneChallenge?> FindChallengeAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Challenges.FirstOrDefault(c => c.Id == id));
        public Task<DateTime?> LatestChallengeAsync(string landlordId, string phone, CancellationToken ct = default) =>
            Task.FromResult(Challenges.Where(c => c.LandlordId == landlordId && c.PhoneNumber == phone).Select(c => (DateTime?)c.CreatedAt).LastOrDefault());
        public Task AddChallengeAsync(TenantPhoneChallenge challenge, CancellationToken ct = default) { Challenges.Add(challenge); return Task.CompletedTask; }
        public Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task RegisterAsync(string landlordId, string login, string phone, Guid challengeId, Func<TenantProfile, AppUser> createUser, CancellationToken ct = default)
        {
            var challenge = Challenges.Single(c => c.Id == challengeId);
            if (challenge.VerifiedAt is null || challenge.ConsumedAt is not null) throw new AppValidationException("Chưa xác minh.");
            var profile = Profiles.Single(p => p.CreatedByUserId == landlordId && p.PhoneNumber == phone);
            Created = createUser(profile);
            profile.UserId = Created.Id;
            challenge.ConsumedAt = DateTime.UtcNow;
            return Task.CompletedTask;
        }

        public Task SelfRegisterAsync(AppUser user, TenantProfile profile, CancellationToken ct = default)
        {
            if (Created is not null && (Created.PhoneNumber == user.PhoneNumber || Created.CitizenId == user.CitizenId))
                throw new AppConflictException("Không thể đăng ký.", "TENANT_REGISTRATION_CONFLICT");
            Created = user;
            Profiles.Add(profile);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task SelfRegister_CreatesUnverifiedIndependentTenant_AndBlocksDuplicateTakeover()
    {
        var store = new FakeStore();
        var service = new TenantRegistrationService(store, new PasswordHasher<AppUser>(), new FakeSms());

        var dto = await service.SelfRegisterAsync(new RoomManagement.Business.DTOs.Users.SelfRegisterTenantDto
        {
            FullName = "Nguyễn Văn An",
            PhoneNumber = "+84901234567",
            CitizenId = "079200012345",
            DateOfBirth = new DateOnly(2000, 1, 15),
            Address = "123 Lê Lợi, Q1",
            Password = "Password123!",
            ConfirmPassword = "Password123!"
        });

        Assert.Equal(UserRoles.Tenant, dto.Role);
        Assert.Equal("0901234567", dto.PhoneNumber);
        Assert.Equal(RoomManagement.Domain.Constants.IdentityVerificationStatuses.Unverified, dto.IdentityVerificationStatus);
        Assert.Single(store.Profiles);
        Assert.Null(store.Profiles[0].OwnerLandlordUserId);
        Assert.Equal(dto.Id, store.Profiles[0].UserId);

        await Assert.ThrowsAsync<AppConflictException>(() => service.SelfRegisterAsync(new RoomManagement.Business.DTOs.Users.SelfRegisterTenantDto
        {
            FullName = "Attacker",
            PhoneNumber = "0901234567",
            CitizenId = "079200099999",
            DateOfBirth = new DateOnly(2000, 1, 15),
            Address = "Anywhere",
            Password = "Password123!",
            ConfirmPassword = "Password123!"
        }));
    }
}
