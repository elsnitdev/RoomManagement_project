using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RoomManagement.Business.Interfaces;
using RoomManagement.Domain;
using RoomManagement.Domain.Constants;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Tests;

public sealed class TenantSelfRegistrationAndLookupApiTests
{
    [Fact]
    public async Task Legacy_otp_endpoints_return_410_gone()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Gone, (await client.PostAsJsonAsync("/api/auth/tenant-registration/check", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Gone, (await client.PostAsJsonAsync("/api/auth/tenant-registration/request-otp", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Gone, (await client.PostAsJsonAsync("/api/auth/tenant-registration/verify-otp", new { })).StatusCode);
    }

    [Fact]
    public async Task Tenant_can_self_register_without_landlord_code_and_view_own_profile()
    {
        await using var factory = new ApiFactory();
        var tenantStore = new InMemoryRegistrationStore(factory.Store);
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<ITenantRegistrationStore>(tenantStore);
            });
        });

        using var client = customFactory.CreateClient();
        var reg = await client.PostAsJsonAsync("/api/auth/register-tenant", new
        {
            fullName = "Trần Thị Bình",
            phoneNumber = "0912345678",
            citizenId = "079201099988",
            dateOfBirth = "1999-05-20",
            address = "Quận 1, TP.HCM",
            password = "Secret123!",
            confirmPassword = "Secret123!"
        });
        Assert.Equal(HttpStatusCode.Created, reg.StatusCode);

        var regJson = await reg.Content.ReadFromJsonAsync<JsonElement>();
        var data = regJson.GetProperty("data");
        Assert.Equal(UserRoles.Tenant, data.GetProperty("role").GetString());
        Assert.Equal(IdentityVerificationStatuses.Unverified, data.GetProperty("identityVerificationStatus").GetString());

        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            userNameOrEmail = "0912345678",
            password = "Secret123!"
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var me = await client.GetAsync("/api/me/profile");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var meJson = await me.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Trần Thị Bình", meJson.GetProperty("data").GetProperty("fullName").GetString());
        Assert.Equal(IdentityVerificationStatuses.Unverified, meJson.GetProperty("data").GetProperty("identityVerificationStatus").GetString());

        // Duplicate registration with same phone or CCCD returns unified 409 conflict
        var dup = await client.PostAsJsonAsync("/api/auth/register-tenant", new
        {
            fullName = "Other",
            phoneNumber = "0912345678",
            citizenId = "079201099988",
            dateOfBirth = "1999-05-20",
            address = "Quận 1",
            password = "Secret123!",
            confirmPassword = "Secret123!"
        });
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
        var dupJson = await dup.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("TENANT_REGISTRATION_CONFLICT", dupJson.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Admin_can_lock_unlock_and_soft_delete_tenant_while_landlord_cannot()
    {
        await using var factory = new ApiFactory();
        var admin = factory.Store.AddUser(UserRoles.Admin);
        var landlord = factory.Store.AddUser(UserRoles.Landlord);
        var tenant = factory.Store.AddUser(UserRoles.Tenant);

        using var client = factory.CreateClient();

        // Landlord cannot access admin user lifecycle endpoints
        await SignInAsync(client, landlord);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/admin/users/{tenant.Id}/lock", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"/api/admin/users/{tenant.Id}")).StatusCode);

        // Admin can lock, unlock, and soft-delete tenant
        await SignInAsync(client, admin);
        var lockResp = await client.PostAsync($"/api/admin/users/{tenant.Id}/lock", null);
        Assert.Equal(HttpStatusCode.OK, lockResp.StatusCode);
        Assert.False(tenant.IsActive);

        var unlockResp = await client.PostAsync($"/api/admin/users/{tenant.Id}/unlock", null);
        Assert.Equal(HttpStatusCode.OK, unlockResp.StatusCode);
        Assert.True(tenant.IsActive);

        var delResp = await client.DeleteAsync($"/api/admin/users/{tenant.Id}");
        Assert.Equal(HttpStatusCode.OK, delResp.StatusCode);
        Assert.False(tenant.IsActive);
        Assert.NotNull(tenant.DeletedAt);
    }

    private static async Task SignInAsync(HttpClient client, AppUser user)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            userNameOrEmail = user.UserName,
            password = TestUserStore.Password
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", json.GetProperty("accessToken").GetString());
    }

    private sealed class InMemoryRegistrationStore : ITenantRegistrationStore
    {
        private readonly TestUserStore _userStore;
        public InMemoryRegistrationStore(TestUserStore userStore) => _userStore = userStore;

        public Task<AppUser?> FindLandlordAsync(string code, CancellationToken ct = default) => Task.FromResult<AppUser?>(null);
        public Task<IReadOnlyList<TenantProfile>> FindByPhoneAsync(string landlordId, string phone, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TenantProfile>>([]);
        public Task<bool> UserNameExistsAsync(string login, CancellationToken ct = default) =>
            _userStore.ExistsByNormalizedUserNameAsync(login.ToUpperInvariant(), ct);
        public Task<TenantPhoneChallenge?> FindChallengeAsync(Guid id, CancellationToken ct = default) => Task.FromResult<TenantPhoneChallenge?>(null);
        public Task<DateTime?> LatestChallengeAsync(string landlordId, string phone, CancellationToken ct = default) => Task.FromResult<DateTime?>(null);
        public Task AddChallengeAsync(TenantPhoneChallenge challenge, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task RegisterAsync(string landlordId, string login, string phone, Guid challengeId, Func<TenantProfile, AppUser> createUser, CancellationToken ct = default)
            => throw new NotSupportedException();

        public async Task SelfRegisterAsync(AppUser user, TenantProfile profile, CancellationToken ct = default)
        {
            if (await _userStore.ExistsByNormalizedUserNameAsync(user.NormalizedUserName, ct) ||
                (!string.IsNullOrEmpty(user.CitizenId) && await _userStore.ExistsByCitizenIdAsync(user.CitizenId, null, ct)))
            {
                throw new RoomManagement.Business.Exceptions.AppConflictException(
                    "Không thể đăng ký.", "TENANT_REGISTRATION_CONFLICT");
            }
            await _userStore.AddAsync(user, ct);
        }
    }
}
