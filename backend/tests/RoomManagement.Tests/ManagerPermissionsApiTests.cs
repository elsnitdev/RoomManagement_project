using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using RoomManagement.Domain;
using RoomManagement.Domain.Constants;

namespace RoomManagement.Tests;

public sealed class ManagerPermissionsApiTests
{
    [Fact]
    public async Task Manager_with_MustChangePassword_is_blocked_from_business_apis_until_password_changed()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var landlord = factory.Store.AddUser(UserRoles.Landlord);
        var manager = factory.Store.AddUser(UserRoles.Manager);
        manager.LandlordUserId = landlord.Id;
        manager.MustChangePassword = true;

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            userNameOrEmail = manager.UserName,
            password = TestUserStore.Password
        });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var loginJson = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(loginJson.GetProperty("user").GetProperty("mustChangePassword").GetBoolean());
        var token = loginJson.GetProperty("accessToken").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Business APIs must reject with 403 PASSWORD_CHANGE_REQUIRED while MustChangePassword == true
        var propListResponse = await client.GetAsync("/api/properties");
        Assert.Equal(HttpStatusCode.Forbidden, propListResponse.StatusCode);
        var problem = await propListResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("PASSWORD_CHANGE_REQUIRED", problem.GetProperty("code").GetString());

        var tenantListResponse = await client.GetAsync("/api/landlord/tenants");
        Assert.Equal(HttpStatusCode.Forbidden, tenantListResponse.StatusCode);

        // Change password endpoint IS allowed
        var newPassword = "NewStrongPassword!99";
        var changeResponse = await client.PostAsJsonAsync("/api/auth/change-password", new
        {
            currentPassword = TestUserStore.Password,
            newPassword,
            confirmPassword = newPassword
        });
        Assert.Equal(HttpStatusCode.OK, changeResponse.StatusCode);
        var changeJson = await changeResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(changeJson.GetProperty("data").GetProperty("mustChangePassword").GetBoolean());

        // Now business APIs are no longer blocked by MustChangePasswordFilter
        // (Landlord-only endpoints still return 403 Forbidden for Manager role)
        var createPropResponse = await client.PostAsJsonAsync("/api/properties", new
        {
            name = "Test",
            address = "Address"
        });
        Assert.Equal(HttpStatusCode.Forbidden, createPropResponse.StatusCode);
    }

    [Fact]
    public async Task Manager_cannot_access_landlord_only_endpoints()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var landlord = factory.Store.AddUser(UserRoles.Landlord);
        var manager = factory.Store.AddUser(UserRoles.Manager);
        manager.LandlordUserId = landlord.Id;
        manager.MustChangePassword = false;

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            userNameOrEmail = manager.UserName,
            password = TestUserStore.Password
        });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var token = (await loginResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var propertyId = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/properties", new { name = "A", address = "B" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"/api/properties/{propertyId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"/api/properties/{propertyId}/payment-account", new
        {
            bankName = "VCB",
            accountNumber = "123456",
            accountHolder = "Test",
            rowVersion = "AAAAAAAA"
        })).StatusCode);

        // Manager cannot view or manage other Managers
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/landlord/managers")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/landlord/managers", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"/api/landlord/managers/{Guid.NewGuid()}")).StatusCode);
    }
}
