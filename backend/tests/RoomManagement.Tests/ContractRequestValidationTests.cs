using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using RoomManagement.Domain;

namespace RoomManagement.Tests;

public sealed class ContractRequestValidationTests
{
    [Theory]
    [InlineData("terminate", "{\"reason\":\"\"}")]
    [InlineData("complete-settlement", "{\"depositDisposition\":\"\",\"refundAmount\":0}")]
    [InlineData("apply-deposit", "{\"invoiceId\":\"00000000-0000-0000-0000-000000000001\",\"amount\":1,\"reason\":\"\",\"idempotencyKey\":\"key\"}")]
    public async Task RecordConstructorValidationReturnsBadRequest(string action, string payload)
    {
        await using var factory = new ApiFactory();
        var landlord = factory.Store.AddUser(UserRoles.Landlord);
        using var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { userNameOrEmail = landlord.Email, password = TestUserStore.Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            auth.GetProperty("accessToken").GetString());

        using var body = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await client.PostAsync(
            $"/api/properties/{Guid.NewGuid()}/contracts/{Guid.NewGuid()}/{action}", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
