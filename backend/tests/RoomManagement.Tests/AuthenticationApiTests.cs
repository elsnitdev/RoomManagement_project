using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using RoomManagement.Domain;
using RoomManagement.Domain.Constants;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Tests;

public sealed class AuthenticationApiTests
{
    [Fact]
    public async Task Protected_endpoints_reject_anonymous_requests()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/users/pending-landlords")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/admin/users", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users/unknown/profile-status")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync("/api/users/unknown/profile", new { })).StatusCode);
        Assert.Empty(factory.Store.LookedUpIds);
    }

    [Theory]
    [InlineData(UserRoles.Tenant)]
    [InlineData(UserRoles.Landlord)]
    public async Task Non_admin_cannot_read_or_modify_admin_resources(string role)
    {
        await using var factory = new ApiFactory();
        var user = factory.Store.AddUser(role);
        using var client = factory.CreateClient();
        await SignInAsync(client, user);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/users/pending-landlords")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/admin/users", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/admin/users/pending-landlords/unknown/approve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/admin/users/pending-landlords/unknown/reject", new { reason = "No" })).StatusCode);
    }

    [Fact]
    public async Task Owner_can_access_profile_but_other_ids_are_rejected_before_lookup()
    {
        await using var factory = new ApiFactory();
        var owner = factory.Store.AddUser();
        var other = factory.Store.AddUser();
        using var client = factory.CreateClient();
        await SignInAsync(client, owner);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/users/{owner.Id}/profile-status")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/users/{other.Id}/profile-status")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"/api/users/{other.Id}/profile", new { fullName = "Unauthorized change" })).StatusCode);
        Assert.DoesNotContain(other.Id, factory.Store.LookedUpIds);
        Assert.Null(other.FullName);
        var update = await client.PutAsJsonAsync($"/api/users/{owner.Id}/profile", new
        {
            fullName = "Test Tenant", phoneNumber = "0912345678", citizenId = "012345678901", dateOfBirth = "1990-01-01"
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal("Test Tenant", owner.FullName);
    }

    [Theory]
    [InlineData(ApprovalStatuses.Pending, false)]
    [InlineData(ApprovalStatuses.Rejected, false)]
    [InlineData(ApprovalStatuses.Pending, true)]
    [InlineData(ApprovalStatuses.Approved, false)]
    public async Task Ineligible_accounts_cannot_login(string status, bool active)
    {
        await using var factory = new ApiFactory();
        var user = factory.Store.AddUser(UserRoles.Landlord, status, active);
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { userNameOrEmail = user.UserName, password = TestUserStore.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Wrong_password_and_unknown_account_return_same_login_error()
    {
        await using var factory = new ApiFactory();
        var user = factory.Store.AddUser();
        using var client = factory.CreateClient();
        var wrong = await client.PostAsJsonAsync("/api/auth/login", new { userNameOrEmail = user.UserName, password = "wrong" });
        var missing = await client.PostAsJsonAsync("/api/auth/login", new { userNameOrEmail = "unknown", password = "wrong" });
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(await wrong.Content.ReadAsStringAsync(), await missing.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("rejected")]
    [InlineData("role-changed")]
    [InlineData("deleted")]
    public async Task Token_is_rejected_after_account_access_changes(string change)
    {
        await using var factory = new ApiFactory();
        var user = factory.Store.AddUser(UserRoles.Admin);
        using var client = factory.CreateClient();
        await SignInAsync(client, user);
        if (change == "inactive") user.IsActive = false;
        if (change == "rejected") user.ApprovalStatus = ApprovalStatuses.Rejected;
        if (change == "role-changed") user.Role = UserRoles.Tenant;
        if (change == "deleted") factory.Store.Users.TryRemove(user.Id, out _);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/users/pending-landlords")).StatusCode);
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("signature")]
    [InlineData("issuer")]
    [InlineData("audience")]
    public async Task Invalid_tokens_are_rejected(string failure)
    {
        await using var factory = new ApiFactory();
        var user = factory.Store.AddUser(UserRoles.Admin);
        var key = failure == "signature" ? new string('x', 64) : ApiFactory.SigningKey;
        var token = new JwtSecurityToken(
            issuer: failure == "issuer" ? "wrong" : "RoomManagement.Tests",
            audience: failure == "audience" ? "wrong" : "RoomManagement.Tests.Client",
            claims: [new Claim("sub", user.Id), new Claim("role", user.Role)],
            notBefore: DateTime.UtcNow.AddHours(-2),
            expires: failure == "expired" ? DateTime.UtcNow.AddHours(-1) : DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/users/pending-landlords")).StatusCode);
    }

    [Fact]
    public async Task Admin_create_response_contains_approved_status_without_password()
    {
        await using var factory = new ApiFactory();
        var admin = factory.Store.AddUser(UserRoles.Admin);
        using var client = factory.CreateClient();
        await SignInAsync(client, admin);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/users/pending-landlords")).StatusCode);
        var response = await client.PostAsJsonAsync("/api/admin/users", new
        {
            userName = "created-user", email = "created@example.com", password = TestUserStore.Password, role = UserRoles.Tenant
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(ApprovalStatuses.Approved, json.GetProperty("data").GetProperty("approvalStatus").GetString());
        Assert.False(json.GetProperty("data").TryGetProperty("passwordHash", out _));
    }

    [Fact]
    public async Task Overlong_email_returns_bad_request_from_both_create_routes()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        await SignInAsync(client, factory.Store.AddUser(UserRoles.Admin));
        var request = new
        {
            userName = "created-user", email = new string('a', 250) + "@example.com", password = TestUserStore.Password,
            confirmPassword = TestUserStore.Password, role = UserRoles.Tenant, fullName = "Test User", phoneNumber = "0912345678",
            citizenId = "012345678901", dateOfBirth = "1990-01-01", address = "Test Address"
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/admin/users", request)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/register-landlord", request)).StatusCode);
    }

    private static async Task SignInAsync(HttpClient client, AppUser user)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { userNameOrEmail = user.Email, password = TestUserStore.Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.GetProperty("accessToken").GetString());
    }
}
