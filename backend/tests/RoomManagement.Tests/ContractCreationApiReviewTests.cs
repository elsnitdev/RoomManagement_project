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

public sealed class ContractCreationApiReviewTests
{
    [SqlServerFact]
    public async Task Capture_creation_cases_for_review()
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


        var results = new List<object>();
        var failedCases = new List<string>();
        await SignInAsync(client, "invitationOwner", "Pass123!");
        var ownerAuth = client.DefaultRequestHeaders.Authorization;
        var start = RoomManagement.Business.Services.VietnamDate.Today.AddDays(2);
        var end = new DateOnly(start.Year, start.Month, 1).AddMonths(2).AddDays(-1);
        var path = $"/api/properties/{propertyId}/contracts";
        Dictionary<string, object?> Input(string code, decimal deposit = 0) => new()
        {
            ["roomId"] = roomId, ["contractCode"] = code, ["startDate"] = start,
            ["endDate"] = end, ["monthlyRent"] = 3000000, ["depositAmount"] = deposit,
            ["paymentDueDay"] = 5, ["tenancyMode"] = "CoTenancy",
            ["members"] = new[] { new { userId = tenantIds[0], isContactPerson = true } }
        };
        async Task<JsonElement?> Call(string id, string description, string url, object? input, int expected, string method = "POST")
        {
            using var req = new HttpRequestMessage(new HttpMethod(method), url);
            if (input != null) req.Content = JsonContent.Create(input);
            using var response = await client.SendAsync(req);
            var raw = await response.Content.ReadAsStringAsync();
            JsonElement? json = null;
            try { json = JsonDocument.Parse(raw).RootElement.Clone(); } catch (JsonException) { }
            object? persisted = null;
            if (response.StatusCode == HttpStatusCode.Created && json?.TryGetProperty("data", out var data) == true)
            {
                var idValue = data.GetProperty("id").GetGuid();
                using var scope = factory.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<RoomManagementDbContext>();
                persisted = new {
                    contract = await db.Contracts.AsNoTracking().Where(x => x.Id == idValue).Select(x => new { x.Status, x.Workflow, x.MonthlyRent, x.DepositAmount }).SingleAsync(),
                    invoices = await db.Invoices.AsNoTracking().Where(x => x.ContractId == idValue).Select(x => new { x.InvoiceType, x.Status, x.TotalAmount }).ToListAsync(),
                    payments = await (from p in db.Payments.AsNoTracking() join i in db.Invoices on p.InvoiceId equals i.Id where i.ContractId == idValue select new { p.Status, p.VerifiedAmount, p.PaymentMethod }).ToListAsync(),
                    members = await db.ContractTenants.CountAsync(x => x.ContractId == idValue),
                    held = await db.ContractDepositBalances.Where(x => x.ContractId == idValue).SumAsync(x => (decimal?)x.DepositHeld) ?? 0
                };
            }
            if ((int)response.StatusCode != expected) failedCases.Add(id);
            results.Add(new { id, description, method, url, input, expectedStatus = expected, actualStatus = (int)response.StatusCode,
                passed = (int)response.StatusCode == expected, output = (object?)json ?? raw, persisted });
            Console.WriteLine($"{id} expected={expected} actual={(int)response.StatusCode}");
            return json;
        }
        var first = await Call("HD-01", "Create without deposit", path, Input("REVIEW-NO-DEPOSIT"), 201);
        await Call("HD-02", "Create with unpaid deposit", path, Input("REVIEW-UNPAID", 3000000), 201);
        var paid = Input("REVIEW-PAID", 3000000); paid["depositPaymentStatus"] = "Paid"; paid["depositPaymentMethod"] = "Cash";
        await Call("HD-03", "Create with paid deposit", path, paid, 201);
        var invalid = Input("REVIEW-BAD-DATES"); invalid["endDate"] = start.AddDays(-1);
        await Call("HD-04A", "End before start", path, invalid, 400);
        invalid = Input("REVIEW-NOT-MONTH-END"); invalid["endDate"] = end.AddDays(-1);
        await Call("HD-04B", "End is not month end", path, invalid, 400);
        invalid = Input("REVIEW-MISSING"); invalid.Remove("contractCode");
        await Call("HD-04C", "Missing code", path, invalid, 400);
        invalid = Input("REVIEW-NEGATIVE"); invalid["monthlyRent"] = -1;
        await Call("HD-04D", "Negative rent", path, invalid, 400);
        await Call("HD-05A", "Second draft same room and period", path, Input("REVIEW-SECOND-DRAFT"), 201);
        var firstId = first!.Value.GetProperty("data").GetProperty("id").GetGuid();
        await Call("HD-05B", "Send first invitation", path + $"/{firstId}/send-offer", null, 200);
        var third = await Call("HD-05C", "Create draft while room reserved", path, Input("REVIEW-RESERVED"), 201);
        if (third?.TryGetProperty("data", out var thirdData) == true)
            await Call("HD-05D", "Send overlapping invitation", path + $"/{thirdData.GetProperty("id").GetGuid()}/send-offer", null, 409);
        await SignInAsync(client, tenantAccounts[0].Phone, "Password123!");
        var offer = await client.GetFromJsonAsync<JsonElement>($"/api/me/contracts/{firstId}");
        var details = offer.GetProperty("data");
        await Call("HD-06", "Representative confirms first invitation", $"/api/me/contracts/{firstId}/confirm",
            new { offerVersion = details.GetProperty("offerVersion").GetInt32(), termsHash = details.GetProperty("offerTermsHash").GetString() }, 200);
        await Call("HD-08A", "Tenant tries to create", path, Input("REVIEW-FORBIDDEN"), 403);
        client.DefaultRequestHeaders.Authorization = null;
        await Call("HD-08B", "Unauthenticated creation", path, Input("REVIEW-ANON"), 401);
        client.DefaultRequestHeaders.Authorization = ownerAuth;
        await Call("HD-08C", "Owner uses foreign property", $"/api/properties/{Guid.NewGuid()}/contracts", Input("REVIEW-FOREIGN"), 404);
        invalid = Input("REVIEW-BAD-MEMBER"); invalid["members"] = new[] { new { userId = "missing-user", isContactPerson = true } };
        await Call("HD-07A", "Unknown member rolls back creation", path, invalid, 404);
        invalid = Input("REVIEW-FAMILY"); invalid["tenancyMode"] = "PrimaryTenantWithFamily";
        invalid["members"] = tenantIds.Take(2).Select((id, n) => new { userId = id, isContactPerson = n == 0 }).ToArray();
        await Call("HD-07B", "Family mode with multiple named tenants", path, invalid, 400);
        invalid = Input("REVIEW-ZERO-PAID"); invalid["depositPaymentStatus"] = "Paid"; invalid["depositPaymentMethod"] = "Cash";
        await Call("HD-03B", "Zero deposit marked paid", path, invalid, 400);
        var extraRoomId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RoomManagementDbContext>();
            db.Rooms.Add(new Room { Id = extraRoomId, PropertyId = propertyId, RoomNumber = "B102", MaxOccupants = 1,
                BasePrice = 3000000, Status = RoomStatus.Available, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        invalid = Input("REVIEW-CAPACITY"); invalid["roomId"] = extraRoomId;
        invalid["members"] = tenantIds.Take(2).Select((id, n) => new { userId = id, isContactPerson = n == 0 }).ToArray();
        await Call("HD-07C", "Exceed room capacity", path, invalid, 400);
        invalid = Input("REVIEW-PAST"); invalid["roomId"] = extraRoomId;
        invalid["startDate"] = new DateOnly(2026, 10, 1); invalid["endDate"] = new DateOnly(2026, 10, 31);
        var pastDateResponse = await Call("HD-04E", "End date before today", path, invalid, 400);
        Assert.Equal("CONTRACT_END_DATE_INVALID", pastDateResponse!.Value.GetProperty("code").GetString());
        var today = RoomManagement.Business.Services.VietnamDate.Today;
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        invalid = Input("REVIEW-PREVIOUS-MONTH"); invalid["roomId"] = extraRoomId;
        invalid["startDate"] = monthStart.AddMonths(-1); invalid["endDate"] = monthStart.AddDays(-1);
        await Call("HD-04F", "Previous month end before today", path, invalid, 400);
        invalid = Input("REVIEW-CURRENT-MONTH"); invalid["roomId"] = extraRoomId;
        invalid["startDate"] = monthStart; invalid["endDate"] = monthStart.AddMonths(1).AddDays(-1);
        await Call("HD-04G", "Past start with current month end", path, invalid, 201);
        invalid = Input("REVIEW-ACTIVE"); invalid["roomId"] = extraRoomId;
        invalid["startDate"] = RoomManagement.Business.Services.VietnamDate.Today;
        var immediate = await Call("HD-06B-CREATE", "Create beginning today", path, invalid, 201);
        var immediateId = immediate!.Value.GetProperty("data").GetProperty("id").GetGuid();
        var sentNow = await Call("HD-06B-SEND", "Send beginning today", path + $"/{immediateId}/send-offer", null, 200);
        var current = sentNow!.Value.GetProperty("data");
        await SignInAsync(client, tenantAccounts[0].Phone, "Password123!");
        await Call("HD-06B", "Representative confirms beginning today", $"/api/me/contracts/{immediateId}/confirm",
            new { offerVersion = current.GetProperty("offerVersion").GetInt32(), termsHash = current.GetProperty("offerTermsHash").GetString() }, 200);
        object rollbackCheck;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RoomManagementDbContext>();
            rollbackCheck = new { invalidMemberContractCount = await db.Contracts.CountAsync(x => x.ContractCode == "REVIEW-BAD-MEMBER"),
                capacityContractCount = await db.Contracts.CountAsync(x => x.ContractCode == "REVIEW-CAPACITY"),
                pastEndContractCount = await db.Contracts.CountAsync(x => x.ContractCode == "REVIEW-PAST" ||
                    x.ContractCode == "REVIEW-PREVIOUS-MONTH") };
            Assert.False(await db.Contracts.AnyAsync(x => x.ContractCode == "REVIEW-PAST" ||
                x.ContractCode == "REVIEW-PREVIOUS-MONTH"));
        }
        var report = new { ranAt = DateTimeOffset.UtcNow, environment = "ASP.NET Core TestServer + SQL Server LocalDB, isolated database, hosted email disabled", propertyId, roomId, start, end, rollbackCheck, results };
        var reportPath = Environment.GetEnvironmentVariable("CONTRACT_REVIEW_OUTPUT");
        if (!string.IsNullOrWhiteSpace(reportPath))
            await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } }));
        Assert.True(failedCases.Count == 0, "Unexpected HTTP results: " + string.Join(", ", failedCases));
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
