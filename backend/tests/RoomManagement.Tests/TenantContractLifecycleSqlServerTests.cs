using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RoomManagement.Data;
using RoomManagement.Domain;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Tests;

public sealed class TenantContractLifecycleSqlServerTests
{
    [SqlServerFact]
    public async Task Full_two_party_offer_lookup_multi_landlord_and_termination_notification_flow()
    {
        await using var db = await SqlServerDatabase.CreateAsync();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Testing");
            b.UseSetting("ConnectionStrings:RoomManagement", db.ConnectionString);
            b.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:RoomManagement"] = db.ConnectionString,
                    ["InitialAdmin:Password"] = "",
                    ["Jwt:Issuer"] = "RoomManagement.Tests",
                    ["Jwt:Audience"] = "RoomManagement.Tests.Client",
                    ["Jwt:SigningKey"] = ApiFactory.SigningKey,
                    ["Jwt:AccessTokenMinutes"] = "15"
                }));
        });

        using var client = factory.CreateClient();

        // 1. Tenant self-registers without LandlordCode or OTP
        var regResp = await client.PostAsJsonAsync("/api/auth/register-tenant", new
        {
            fullName = "Phạm Minh Tuấn",
            phoneNumber = "0987654321",
            citizenId = "079200055566",
            dateOfBirth = "1997-03-10",
            address = "Quận 7, TP.HCM",
            password = "Password123!",
            confirmPassword = "Password123!"
        });
        Assert.Equal(HttpStatusCode.Created, regResp.StatusCode);

        // 2. Seed Landlord A, Landlord B, Property A, Room A, Rules A, Property B, Room B, Rules B
        string landlordAId, landlordBId;
        Guid propAId, roomAId, propBId, roomBId;
        using (var scope = factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<RoomManagementDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.IPasswordHasher<AppUser>>();

            var landlordA = new AppUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = "landlordA",
                NormalizedUserName = "LANDLORDA",
                Role = UserRoles.Landlord,
                IsActive = true,
                LandlordCode = "LA00000001",
                CreatedAt = DateTime.UtcNow
            };
            landlordA.PasswordHash = hasher.HashPassword(landlordA, "Pass123!");

            var landlordB = new AppUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = "landlordB",
                NormalizedUserName = "LANDLORDB",
                Role = UserRoles.Landlord,
                IsActive = true,
                LandlordCode = "LB00000001",
                CreatedAt = DateTime.UtcNow
            };
            landlordB.PasswordHash = hasher.HashPassword(landlordB, "Pass123!");

            ctx.Users.AddRange(landlordA, landlordB);
            landlordAId = landlordA.Id;
            landlordBId = landlordB.Id;

            propAId = Guid.NewGuid();
            roomAId = Guid.NewGuid();
            propBId = Guid.NewGuid();
            roomBId = Guid.NewGuid();

            ctx.Properties.AddRange(
                new Property { Id = propAId, OwnerUserId = landlordAId, Name = "Nhà trọ A", Address = "Addr A", CreatedAt = DateTime.UtcNow },
                new Property { Id = propBId, OwnerUserId = landlordBId, Name = "Nhà trọ B", Address = "Addr B", CreatedAt = DateTime.UtcNow });
            ctx.Rooms.AddRange(
                new Room { Id = roomAId, PropertyId = propAId, RoomNumber = "A101", MaxOccupants = 2, BasePrice = 3000000, Status = RoomStatus.Available, CreatedAt = DateTime.UtcNow },
                new Room { Id = roomBId, PropertyId = propBId, RoomNumber = "B101", MaxOccupants = 2, BasePrice = 3500000, Status = RoomStatus.Available, CreatedAt = DateTime.UtcNow });
            ctx.ContractRulesVersions.AddRange(
                new ContractRulesVersion { Id = Guid.NewGuid(), PropertyId = propAId, VersionNumber = 1, Content = "Nội quy A", IsActive = true, CreatedByUserId = landlordAId, CreatedAt = DateTime.UtcNow },
                new ContractRulesVersion { Id = Guid.NewGuid(), PropertyId = propBId, VersionNumber = 1, Content = "Nội quy B", IsActive = true, CreatedByUserId = landlordBId, CreatedAt = DateTime.UtcNow });

            await ctx.SaveChangesAsync();
        }

        // 3. Landlord A logs in -> before contract, tenant list does NOT expose self-registered tenant
        await SignInAsync(client, "landlordA", "Pass123!");
        var listBefore = await client.GetFromJsonAsync<JsonElement>("/api/landlord/tenants");
        Assert.Equal(0, listBefore.GetProperty("meta").GetProperty("total").GetInt32());

        // Mismatch lookup returns 404 unified error
        var mismatch = await client.PostAsJsonAsync("/api/landlord/tenants/lookup", new
        {
            propertyId = propAId,
            phoneNumber = "0987654321",
            citizenId = "079200000000"
        });
        Assert.Equal(HttpStatusCode.NotFound, mismatch.StatusCode);

        // Exact match returns minimal masked info
        var lookupResp = await client.PostAsJsonAsync("/api/landlord/tenants/lookup", new
        {
            propertyId = propAId,
            phoneNumber = "0987654321",
            citizenId = "079200055566"
        });
        Assert.Equal(HttpStatusCode.OK, lookupResp.StatusCode);
        var lookupData = (await lookupResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        var tenantUserId = lookupData.GetProperty("userId").GetString()!;
        Assert.DoesNotContain("079200055566", lookupResp.Content.ReadAsStringAsync().Result);

        // Looking up does NOT grant access in /api/landlord/tenants
        var listAfterLookup = await client.GetFromJsonAsync<JsonElement>("/api/landlord/tenants");
        Assert.Equal(0, listAfterLookup.GetProperty("meta").GetProperty("total").GetInt32());

        // 4. Create Draft contract, attach tenant by UserId, send offer
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var createContract = await client.PostAsJsonAsync($"/api/properties/{propAId}/contracts", new
        {
            roomId = roomAId,
            contractCode = "HD-A101",
            startDate = today.ToString("yyyy-MM-dd"),
            endDate = new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month)).ToString("yyyy-MM-dd"),
            monthlyRent = 3000000,
            depositAmount = 3000000,
            tenancyMode = "CoTenancy"
        });
        Assert.True(createContract.StatusCode == HttpStatusCode.Created,
            $"Create legacy contract: {createContract.StatusCode} {await createContract.Content.ReadAsStringAsync()}");
        var contractId = (await createContract.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetGuid();
        // This already-sent contract exercises the preserved legacy rules workflow.
        using (var scope = factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<RoomManagementDbContext>();
            await ctx.Contracts.Where(x => x.Id == contractId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Workflow, ContractWorkflow.LegacyRules));
        }

        var addTenant = await client.PostAsJsonAsync($"/api/properties/{propAId}/contracts/{contractId}/tenants/by-user", new
        {
            userId = tenantUserId,
            joinDate = today.ToString("yyyy-MM-dd"),
            isContactPerson = true,
            role = "ContractParty"
        });
        Assert.Equal(HttpStatusCode.Created, addTenant.StatusCode);

        // Cannot activate Draft before sending & tenant confirming
        var earlyActivate = await client.PostAsync($"/api/properties/{propAId}/contracts/{contractId}/activate", null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, earlyActivate.StatusCode);

        // Send offer -> PendingTenantConfirmation
        var sendOffer = await client.PostAsync($"/api/properties/{propAId}/contracts/{contractId}/send-offer", null);
        Assert.Equal(HttpStatusCode.OK, sendOffer.StatusCode);
        var sentData = (await sendOffer.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        var offerVersion = sentData.GetProperty("offerVersion").GetInt32();
        var termsHash = sentData.GetProperty("offerTermsHash").GetString()!;
        var rulesVersionId = sentData.GetProperty("offerRulesVersionId").GetGuid();

        // 5. Tenant logs in -> sees offer, confirms contract + rules
        await SignInAsync(client, "0987654321", "Password123!");
        var confirm = await client.PostAsJsonAsync($"/api/me/contracts/{contractId}/confirm", new
        {
            offerVersion,
            termsHash,
            rulesVersionId,
            acceptRules = true
        });
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        // Tenant cannot mutate identity fields after confirmation
        var mutateIdentity = await client.PutAsJsonAsync("/api/me/profile", new
        {
            fullName = "Tên Khác",
            phoneNumber = "0987654321",
            citizenId = "079200055566",
            dateOfBirth = "1997-03-10",
            address = "Quận 7, TP.HCM"
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, mutateIdentity.StatusCode);

        // 6. A newer rules version requires a new offer and tenant confirmation.
        await SignInAsync(client, "landlordA", "Pass123!");
        var changedRules = await client.PutAsJsonAsync($"/api/properties/{propAId}/rules", new
        {
            content = "Nội quy A phiên bản hai",
            expectedVersionId = rulesVersionId
        });
        Assert.Equal(HttpStatusCode.OK, changedRules.StatusCode);
        var newRulesVersionId = (await changedRules.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data").GetProperty("versionId").GetGuid();
        Assert.NotEqual(rulesVersionId, newRulesVersionId);

        var staleActivate = await client.PostAsync($"/api/properties/{propAId}/contracts/{contractId}/activate", null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, staleActivate.StatusCode);

        var resentOffer = await client.PostAsync($"/api/properties/{propAId}/contracts/{contractId}/send-offer", null);
        Assert.Equal(HttpStatusCode.OK, resentOffer.StatusCode);
        var resentData = (await resentOffer.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal(offerVersion + 1, resentData.GetProperty("offerVersion").GetInt32());
        Assert.Equal(newRulesVersionId, resentData.GetProperty("offerRulesVersionId").GetGuid());

        await SignInAsync(client, "0987654321", "Password123!");
        var reconfirm = await client.PostAsJsonAsync($"/api/me/contracts/{contractId}/confirm", new
        {
            offerVersion = resentData.GetProperty("offerVersion").GetInt32(),
            termsHash = resentData.GetProperty("offerTermsHash").GetString(),
            rulesVersionId = newRulesVersionId,
            acceptRules = true
        });
        Assert.Equal(HttpStatusCode.OK, reconfirm.StatusCode);

        // Landlord A countersigns -> Active, and terminates -> Tenant receives notification.
        await SignInAsync(client, "landlordA", "Pass123!");
        var activate = await client.PostAsync($"/api/properties/{propAId}/contracts/{contractId}/activate", null);
        Assert.Equal(HttpStatusCode.OK, activate.StatusCode);

        var terminate = await client.PostAsJsonAsync($"/api/properties/{propAId}/contracts/{contractId}/terminate", new
        {
            reason = "Chuyển công tác"
        });
        Assert.Equal(HttpStatusCode.OK, terminate.StatusCode);

        // Tenant sees termination notification
        await SignInAsync(client, "0987654321", "Password123!");
        var notificationsResponse = await client.GetAsync("/api/me/notifications");
        Assert.True(notificationsResponse.IsSuccessStatusCode,
            $"Notifications: {notificationsResponse.StatusCode} {await notificationsResponse.Content.ReadAsStringAsync()}");
        var notifs = await notificationsResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(notifs.TryGetProperty("meta", out var meta), $"Notifications: {notifs}");
        Assert.True(meta.GetProperty("totalItems").GetInt32() >= 2,
            $"Notifications: {notifs}");
    }

    private static async Task SignInAsync(HttpClient client, string login, string password)
    {
        var resp = await client.PostAsJsonAsync("/api/auth/login", new { userNameOrEmail = login, password });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.GetProperty("accessToken").GetString());
    }
}
