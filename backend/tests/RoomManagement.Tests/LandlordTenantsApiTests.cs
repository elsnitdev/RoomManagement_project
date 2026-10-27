using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using RoomManagement.Domain;
using RoomManagement.Domain.Constants;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Tests;

/// <summary>
/// Integration tests kiểm tra các API endpoint của LandlordTenantsController (/api/landlord/tenants).
/// </summary>
public sealed class LandlordTenantsApiTests
{
    [Fact]
    public async Task Anonymous_request_returns_401_unauthorized()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var getResponse = await client.GetAsync("/api/landlord/tenants");
        Assert.Equal(HttpStatusCode.Unauthorized, getResponse.StatusCode);

        var postResponse = await client.PostAsJsonAsync("/api/landlord/tenants", new { fullName = "A", phoneNumber = "0901234567" });
        Assert.Equal(HttpStatusCode.Unauthorized, postResponse.StatusCode);
    }

    [Fact]
    public async Task Tenant_role_request_returns_403_forbidden()
    {
        await using var factory = new ApiFactory();
        var tenantUser = factory.Store.AddUser(UserRoles.Tenant);
        using var client = factory.CreateClient();
        await SignInAsync(client, tenantUser);

        var getResponse = await client.GetAsync("/api/landlord/tenants");
        Assert.Equal(HttpStatusCode.Forbidden, getResponse.StatusCode);

        var postResponse = await client.PostAsJsonAsync("/api/landlord/tenants", new { fullName = "A", phoneNumber = "0901234567" });
        Assert.Equal(HttpStatusCode.Forbidden, postResponse.StatusCode);
    }

    [Fact]
    public async Task Landlord_can_create_tenant_profile_successfully()
    {
        await using var factory = new ApiFactory();
        var landlordUser = factory.Store.AddUser(UserRoles.Landlord);
        using var client = factory.CreateClient();
        await SignInAsync(client, landlordUser);

        var request = new
        {
            fullName = "Lê Thị Hoa",
            phoneNumber = "0934567890",
            email = "hoa@example.com",
            citizenId = "079201012345",
            dateOfBirth = "1998-10-15",
            gender = "Female",
            permanentAddress = "Quận 3, TP.HCM"
        };

        var response = await client.PostAsJsonAsync("/api/landlord/tenants", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("data", out var data));
        Assert.Equal("Lê Thị Hoa", data.GetProperty("fullName").GetString());
        Assert.Equal("0934567890", data.GetProperty("phoneNumber").GetString());
        Assert.Equal(landlordUser.Id, data.GetProperty("createdByUserId").GetString());
        Assert.Equal(JsonValueKind.Null, data.GetProperty("userId").ValueKind); // Người thuê chưa có tài khoản
        Assert.True(json.TryGetProperty("message", out var message));
        Assert.Contains("thành công", message.GetString());
    }

    [Fact]
    public async Task Create_tenant_profile_with_invalid_phone_returns_400_bad_request()
    {
        await using var factory = new ApiFactory();
        var landlordUser = factory.Store.AddUser(UserRoles.Landlord);
        using var client = factory.CreateClient();
        await SignInAsync(client, landlordUser);

        var request = new
        {
            fullName = "Trần Văn Sai",
            phoneNumber = "123456" // Sai định dạng
        };

        var response = await client.PostAsJsonAsync("/api/landlord/tenants", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("VALIDATION_FAILED", json.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Create_tenant_profile_with_duplicate_citizen_id_returns_409_conflict()
    {
        await using var factory = new ApiFactory();
        var landlordUser = factory.Store.AddUser(UserRoles.Landlord);

        // Đã có sẵn hồ sơ với CCCD
        await factory.TenantProfileStore.AddAsync(new TenantProfile
        {
            Id = Guid.NewGuid(),
            CreatedByUserId = landlordUser.Id,
            FullName = "Người Đã Có",
            PhoneNumber = "0901111111",
            CitizenId = "079201012345",
            CreatedAt = DateTime.UtcNow
        });

        using var client = factory.CreateClient();
        await SignInAsync(client, landlordUser);

        var request = new
        {
            fullName = "Người Mới Trùng CCCD",
            phoneNumber = "0902222222",
            citizenId = "079201012345"
        };

        var response = await client.PostAsJsonAsync("/api/landlord/tenants", request);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("CITIZEN_ID_CONFLICT", json.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Get_list_returns_paged_envelope_with_data_and_meta()
    {
        await using var factory = new ApiFactory();
        var landlordUser = factory.Store.AddUser(UserRoles.Landlord);

        await factory.TenantProfileStore.AddAsync(new TenantProfile
        {
            Id = Guid.NewGuid(),
            CreatedByUserId = landlordUser.Id,
            FullName = "Khách 1",
            PhoneNumber = "0901111111",
            CreatedAt = DateTime.UtcNow
        });
        await factory.TenantProfileStore.AddAsync(new TenantProfile
        {
            Id = Guid.NewGuid(),
            CreatedByUserId = landlordUser.Id,
            FullName = "Khách 2",
            PhoneNumber = "0902222222",
            CreatedAt = DateTime.UtcNow
        });

        using var client = factory.CreateClient();
        await SignInAsync(client, landlordUser);

        var response = await client.GetAsync("/api/landlord/tenants?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array);
        Assert.Equal(2, data.GetArrayLength());
        Assert.True(json.TryGetProperty("meta", out var meta));
        Assert.Equal(2, meta.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Landlord_can_get_by_id_and_update_profile()
    {
        await using var factory = new ApiFactory();
        var landlordUser = factory.Store.AddUser(UserRoles.Landlord);

        var profileId = Guid.NewGuid();
        await factory.TenantProfileStore.AddAsync(new TenantProfile
        {
            Id = profileId,
            CreatedByUserId = landlordUser.Id,
            FullName = "Khách Cũ",
            PhoneNumber = "0901111111",
            CreatedAt = DateTime.UtcNow
        });

        using var client = factory.CreateClient();
        await SignInAsync(client, landlordUser);

        // 1. GET By ID
        var getResponse = await client.GetAsync($"/api/landlord/tenants/{profileId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var getJson = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Khách Cũ", getJson.GetProperty("data").GetProperty("fullName").GetString());

        // 2. PUT Update
        var updateRequest = new
        {
            fullName = "Khách Đã Sửa",
            phoneNumber = "0909999999",
            email = "updated@example.com"
        };
        var updateResponse = await client.PutAsJsonAsync($"/api/landlord/tenants/{profileId}", updateRequest);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updateJson = await updateResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Khách Đã Sửa", updateJson.GetProperty("data").GetProperty("fullName").GetString());
        Assert.Equal("0909999999", updateJson.GetProperty("data").GetProperty("phoneNumber").GetString());
    }

    private static async Task SignInAsync(HttpClient client, AppUser user)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            userNameOrEmail = user.Email,
            password = TestUserStore.Password
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", json.GetProperty("accessToken").GetString());
    }
}
