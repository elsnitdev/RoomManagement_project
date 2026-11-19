using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RoomManagement.Business.Interfaces;
using RoomManagement.Data;
using RoomManagement.Domain;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Tests;

public sealed class EarlyExitMeterPrerequisiteApiTests
{
    [SqlServerFact]
    public async Task Earlier_months_must_be_complete_and_final_opening_must_match_before_handover()
    {
        var startOffsetDays = 2;
        await using var database = await SqlServerDatabase.CreateAsync();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:RoomManagement", database.ConnectionString);
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:RoomManagement"] = database.ConnectionString,
                    ["InitialAdmin:Password"] = "",
                    ["Jwt:Issuer"] = "RoomManagement.Tests",
                    ["Jwt:Audience"] = "RoomManagement.Tests.Client",
                    ["Jwt:SigningKey"] = ApiFactory.SigningKey,
                    ["Jwt:AccessTokenMinutes"] = "15"
                }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<Microsoft.Extensions.Hosting.IHostedService>();
                services.RemoveAll<IObjectStorage>();
                services.AddSingleton<IObjectStorage, TestStorage>();
            });
        });
        using var client = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
        {
            var expected = new SqlConnectionStringBuilder(database.ConnectionString);
            var actual = new SqlConnectionStringBuilder(scope.ServiceProvider.GetRequiredService<RoomManagementDbContext>()
                .Database.GetDbConnection().ConnectionString);
            Assert.Equal(expected.DataSource, actual.DataSource);
            Assert.Equal(expected.InitialCatalog, actual.InitialCatalog);
        }

        var tenantAccounts = new[]
        {
            (Name: "Người Đại Diện", Phone: "0987654301", Citizen: "079200055501"),
            (Name: "Người Thứ Hai", Phone: "0987654302", Citizen: "079200055502"),
            (Name: "Người Thứ Ba", Phone: "0987654303", Citizen: "079200055503")
        };
        foreach (var tenant in tenantAccounts)
        {
            var response = await client.PostAsJsonAsync("/api/auth/register-tenant", new
            {
                fullName = tenant.Name, phoneNumber = tenant.Phone, citizenId = tenant.Citizen,
                dateOfBirth = "1997-03-10", address = "TP.HCM", password = "Password123!",
                confirmPassword = "Password123!"
            });
            Assert.True(response.StatusCode == HttpStatusCode.Created,
                $"Registration {tenant.Phone}: {response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        }

        var propertyId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        Guid? existingRulesId = null;
        string[] tenantIds;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RoomManagementDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.IPasswordHasher<AppUser>>();
            var owner = new AppUser
            {
                Id = Guid.NewGuid().ToString(), UserName = "invitationOwner", NormalizedUserName = "INVITATIONOWNER",
                Role = UserRoles.Landlord, IsActive = true, LandlordCode = "LI00000001", CreatedAt = DateTime.UtcNow
            };
            owner.PasswordHash = hasher.HashPassword(owner, "Pass123!");
            db.Users.Add(owner);
            db.Properties.Add(new Property { Id = propertyId, OwnerUserId = owner.Id,
                Name = "Khu trọ không nội quy điện tử", Address = "TP.HCM", CreatedAt = DateTime.UtcNow });
            db.Rooms.Add(new Room { Id = roomId, PropertyId = propertyId, RoomNumber = "A101",
                MaxOccupants = 3, BasePrice = 3000000, Status = RoomStatus.Available, CreatedAt = DateTime.UtcNow });
            if (startOffsetDays == 0)
            {
                existingRulesId = Guid.NewGuid();
                db.ContractRulesVersions.Add(new ContractRulesVersion
                {
                    Id = existingRulesId.Value, PropertyId = propertyId, VersionNumber = 1,
                    Content = "Nội quy cũ", IsActive = true, CreatedByUserId = owner.Id,
                    CreatedAt = DateTime.UtcNow
                });
            }
            await db.SaveChangesAsync();
            tenantIds = await db.Users.Where(x => tenantAccounts.Select(t => t.Phone).Contains(x.PhoneNumber!))
                .OrderBy(x => x.PhoneNumber).Select(x => x.Id).ToArrayAsync();
        }
        Assert.Equal(3, tenantIds.Length);




        await SignInAsync(client, "invitationOwner", "Pass123!");
        var today = RoomManagement.Business.Services.VietnamDate.Today;
        var month = new DateOnly(today.Year, today.Month, 1);
        var previous = month.AddMonths(-1);
        var contractId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RoomManagementDbContext>();
            db.Contracts.Add(new Contract { Id = contractId, RoomId = roomId, ContractCode = "EXIT-PREREQ",
                StartDate = previous, EndDate = month.AddMonths(2).AddDays(-1), Status = ContractStatus.PendingMoveOut,
                MonthlyRent = 3000000, PaymentDueDay = 5, CreatedAt = DateTime.UtcNow });
            var profile = await db.TenantProfiles.SingleAsync(x => x.UserId == tenantIds[0]);
            db.ContractTenants.Add(new ContractTenant { Id = Guid.NewGuid(), ContractId = contractId,
                TenantId = profile.Id, JoinDate = previous, IsContactPerson = true, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        var root = $"/api/properties/{propertyId}/contracts/{contractId}";
        var roomReadings = $"/api/properties/{propertyId}/rooms/{roomId}/meter-readings";
        var prerequisitesPath = root + $"/meter-reading-prerequisites?year={month.Year}&month={month.Month}";
        async Task<JsonElement> Prerequisites()
        {
            var response = await client.GetAsync(prerequisitesPath);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").Clone();
        }
        async Task<HttpResponseMessage> Final(string type, decimal opening, decimal closing) =>
            await client.PostAsJsonAsync(root + "/meter-readings", new { type, year = month.Year, month = month.Month, previousValue = opening, currentValue = closing });
        var initial = await Prerequisites();
        Assert.Equal(2, initial.GetProperty("missingPeriods")[0].GetProperty("missingTypes").GetArrayLength());
        var openingMonth = await client.GetFromJsonAsync<JsonElement>(root +
            $"/meter-reading-prerequisites?year={previous.Year}&month={previous.Month}");
        Assert.Empty(openingMonth.GetProperty("data").GetProperty("missingPeriods").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, openingMonth.GetProperty("data").GetProperty("electricityPreviousValue").ValueKind);
        var blocked = await Final("Electricity", 120, 125);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Equal("CONTRACT_PREVIOUS_READINGS_MISSING", (await blocked.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        var blockedHandover = await client.PostAsJsonAsync(root + "/handover", new { handoverOn = today });
        Assert.Equal(HttpStatusCode.Conflict, blockedHandover.StatusCode);
        Assert.Equal("CONTRACT_PREVIOUS_READINGS_MISSING", (await blockedHandover.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(roomReadings, new {
            type = "Electricity", year = previous.Year, month = previous.Month, previousValue = 100, currentValue = 120 })).StatusCode);
        var missingWater = await Prerequisites();
        Assert.Equal("Water", missingWater.GetProperty("missingPeriods")[0].GetProperty("missingTypes")[0].GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await Final("Electricity", 120, 125)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(roomReadings, new {
            type = "Water", year = previous.Year, month = previous.Month, previousValue = 30, currentValue = 33 })).StatusCode);
        var ready = await Prerequisites();
        Assert.Empty(ready.GetProperty("missingPeriods").EnumerateArray());
        Assert.Equal(120, ready.GetProperty("electricityPreviousValue").GetDecimal());
        Assert.Equal(33, ready.GetProperty("waterPreviousValue").GetDecimal());
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RoomManagementDbContext>();
            var contract = await db.Contracts.SingleAsync(x => x.Id == contractId);
            contract.StartDate = previous.AddMonths(-1);
            await db.SaveChangesAsync();
        }
        var olderGap = await Prerequisites();
        Assert.Single(olderGap.GetProperty("missingPeriods").EnumerateArray());
        Assert.Equal(previous.AddMonths(-1).Month, olderGap.GetProperty("missingPeriods")[0].GetProperty("month").GetInt32());
        Assert.Equal(HttpStatusCode.Conflict, (await Final("Electricity", 120, 125)).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RoomManagementDbContext>();
            var contract = await db.Contracts.SingleAsync(x => x.Id == contractId);
            contract.StartDate = previous;
            await db.SaveChangesAsync();
        }
        var mismatch = await Final("Electricity", 119, 125);
        Assert.Equal(HttpStatusCode.Conflict, mismatch.StatusCode);
        Assert.Equal("CONTRACT_READING_PREVIOUS_MISMATCH", (await mismatch.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Created, (await Final("Electricity", 120, 125)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Final("Water", 33, 34)).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RoomManagementDbContext>();
            Assert.Empty(await db.Invoices.Where(x => x.ContractId == contractId).ToListAsync());
            var final = await db.MeterReadings.SingleAsync(x => x.ContractId == contractId && x.MeterType == UtilityType.Electricity);
            final.PreviousValue = 119; // Existing inconsistent data must also be blocked at handover.
            await db.SaveChangesAsync();
        }
        var oldMismatch = await client.PostAsJsonAsync(root + "/handover", new { handoverOn = today });
        Assert.Equal(HttpStatusCode.Conflict, oldMismatch.StatusCode);
        Assert.Equal("CONTRACT_READING_PREVIOUS_MISMATCH", (await oldMismatch.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RoomManagementDbContext>();
            var contract = await db.Contracts.SingleAsync(x => x.Id == contractId);
            Assert.Equal(ContractStatus.PendingMoveOut, contract.Status);
            var reading = await db.MeterReadings.SingleAsync(x => x.ContractId == contractId && x.MeterType == UtilityType.Electricity);
            reading.PreviousValue = 120;
            await db.SaveChangesAsync();
        }
        var handover = await client.PostAsJsonAsync(root + "/handover", new { handoverOn = today });
        Assert.Equal(HttpStatusCode.OK, handover.StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RoomManagementDbContext>();
            var contract = await db.Contracts.AsNoTracking().SingleAsync(x => x.Id == contractId);
            Assert.Equal(ContractStatus.PendingSettlement, contract.Status);
            Assert.Equal(today, contract.HandoverOn);
        }
    }
    [SqlServerFact]
    public async Task Inline_missing_month_rejects_concurrent_and_stale_writes()
    {
        await using var database = await SqlServerDatabase.CreateAsync();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:RoomManagement", database.ConnectionString);
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> {
                ["ConnectionStrings:RoomManagement"] = database.ConnectionString,
                ["InitialAdmin:Password"] = "", ["Jwt:Issuer"] = "RoomManagement.Tests",
                ["Jwt:Audience"] = "RoomManagement.Tests.Client", ["Jwt:SigningKey"] = ApiFactory.SigningKey
            }));
            builder.ConfigureTestServices(services => services.RemoveAll<Microsoft.Extensions.Hosting.IHostedService>());
        });
        using var client = factory.CreateClient();
        var month = new DateOnly(2026, 11, 1); var previous = month.AddMonths(-1);
        var propertyId = Guid.NewGuid(); var roomId = Guid.NewGuid(); var contractId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RoomManagementDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.IPasswordHasher<AppUser>>();
            var user = new AppUser { Id = Guid.NewGuid().ToString(), UserName = "inlineOwner", NormalizedUserName = "INLINEOWNER",
                Role = UserRoles.Landlord, IsActive = true, CreatedAt = DateTime.UtcNow };
            user.PasswordHash = hasher.HashPassword(user, "Pass123!");
            db.Users.Add(user);
            db.Properties.Add(new Property { Id = propertyId, OwnerUserId = user.Id, Name = "Inline test", Address = "TP.HCM", CreatedAt = DateTime.UtcNow });
            db.Rooms.Add(new Room { Id = roomId, PropertyId = propertyId, RoomNumber = "I01", MaxOccupants = 3, BasePrice = 3000000, CreatedAt = DateTime.UtcNow });
            db.Contracts.Add(new Contract { Id = contractId, RoomId = roomId, ContractCode = "INLINE-READINGS", StartDate = previous,
                EndDate = month.AddMonths(6).AddDays(-1), Status = ContractStatus.PendingMoveOut, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        await SignInAsync(client, "inlineOwner", "Pass123!");
        var root = $"/api/properties/{propertyId}/contracts/{contractId}";
        async Task<JsonElement> Load(DateOnly period) => (await client.GetFromJsonAsync<JsonElement>(root +
            $"/meter-reading-prerequisites?year={period.Year}&month={period.Month}")).GetProperty("data").Clone();
        Task<HttpResponseMessage> Save(DateOnly period, string type, decimal start, decimal end, string version) =>
            client.PostAsJsonAsync(root + "/meter-readings", new { type, year = period.Year, month = period.Month,
                previousValue = start, currentValue = end, expectedVersion = version });
        var initial = await Load(previous);
        var version = initial.GetProperty("version").GetString()!;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<HttpResponseMessage> Writer() { await gate.Task; return await Save(previous, "Electricity", 100, 120, version); }
        var first = Writer(); var second = Writer(); gate.SetResult();
        var responses = await Task.WhenAll(first, second);
        Assert.Single(responses.Where(x => x.StatusCode == HttpStatusCode.Created));
        Assert.Single(responses.Where(x => x.StatusCode == HttpStatusCode.Conflict));
        var failed = responses.Single(x => x.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal("CONTRACT_READINGS_CHANGED", (await failed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        var staleWater = await Save(previous, "Water", 30, 33, version);
        Assert.Equal(HttpStatusCode.Conflict, staleWater.StatusCode);
        var refreshed = await Load(previous);
        Assert.Equal(120, refreshed.GetProperty("electricityReading").GetProperty("currentValue").GetDecimal());
        Assert.Equal(HttpStatusCode.Created, (await Save(previous, "Water", 30, 33, refreshed.GetProperty("version").GetString()!)).StatusCode);
        var ready = await Load(month);
        Assert.Empty(ready.GetProperty("missingPeriods").EnumerateArray());
        Assert.Equal(120, ready.GetProperty("electricityPreviousValue").GetDecimal());
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RoomManagementDbContext>();
            var old = await db.MeterReadings.SingleAsync(x => x.ContractId == contractId && x.MeterType == UtilityType.Electricity);
            old.CurrentValue = 121;
            await db.SaveChangesAsync();
        }
        var staleOpening = await Save(month, "Electricity", 120, 125, ready.GetProperty("version").GetString()!);
        Assert.Equal(HttpStatusCode.Conflict, staleOpening.StatusCode);
        Assert.Equal("CONTRACT_READINGS_CHANGED", (await staleOpening.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        var newest = await Load(month);
        Assert.Equal(121, newest.GetProperty("electricityPreviousValue").GetDecimal());
        Assert.Equal(HttpStatusCode.Created, (await Save(month, "Electricity", 121, 125, newest.GetProperty("version").GetString()!)).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RoomManagementDbContext>();
            Assert.Equal(3, await db.MeterReadings.CountAsync(x => x.ContractId == contractId));
            Assert.Equal(121, (await db.MeterReadings.SingleAsync(x => x.ContractId == contractId && x.Month == month.Month)).PreviousValue);
        }
    }

    private static async Task SignInAsync(HttpClient client, string login, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { userNameOrEmail = login, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.GetProperty("accessToken").GetString());
    }

    private sealed class TestStorage : IObjectStorage
    {
        private readonly ConcurrentDictionary<string, byte[]> files = new();
        public async Task UploadAsync(string name, Stream content, string contentType, CancellationToken ct)
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, ct);
            files[name] = buffer.ToArray();
        }
        public Task<Stream> OpenReadAsync(string name, CancellationToken ct) =>
            Task.FromResult<Stream>(new MemoryStream(files[name]));
        public Task DeleteAsync(string name, CancellationToken ct)
        { files.TryRemove(name, out _); return Task.CompletedTask; }
    }
}
