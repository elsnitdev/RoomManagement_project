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

public sealed class FinalSettlementFinanceApiTests
{
    [SqlServerFact]
    public async Task Final_reconciliation_limits_deposit_and_records_additional_charges_and_discounts()
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
        var contractId = Guid.NewGuid();
        var monthlyId = Guid.NewGuid(); var damageId = Guid.NewGuid(); var depositId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RoomManagementDbContext>();
            var ownerId = (await db.Properties.SingleAsync(x => x.Id == propertyId)).OwnerUserId;
            db.Contracts.Add(new Contract { Id = contractId, RoomId = roomId, ContractCode = "FINAL-FINANCE",
                StartDate = month, EndDate = month.AddMonths(2).AddDays(-1), HandoverOn = today,
                MonthlyRent = 2000000, DepositAmount = 1000000, PaymentDueDay = 5,
                Status = ContractStatus.PendingSettlement, CreatedAt = DateTime.UtcNow });
            foreach (var item in new[] { (depositId, InvoiceType.Deposit, InvoiceItemType.Deposit, 1000000m),
                (monthlyId, InvoiceType.Monthly, InvoiceItemType.Rent, 2000000m),
                (damageId, InvoiceType.Damage, InvoiceItemType.Damage, 500000m) })
            {
                db.Invoices.Add(new Invoice { Id = item.Item1, ContractId = contractId, RoomId = roomId,
                    InvoiceCode = "FIN-" + item.Item1.ToString("N"), InvoiceType = item.Item2,
                    BillingYear = item.Item2 == InvoiceType.Monthly ? (short)today.Year : null,
                    BillingMonth = item.Item2 == InvoiceType.Monthly ? (byte)today.Month : null,
                    IssueDate = today, DueDate = today.AddDays(7), TotalAmount = item.Item4,
                    Status = InvoiceStatus.Issued, CreatedAt = DateTime.UtcNow });
                db.InvoiceItems.Add(new InvoiceItem { Id = Guid.NewGuid(), InvoiceId = item.Item1, ItemType = item.Item3,
                    Description = "Test final reconciliation", Quantity = 1, UnitPrice = item.Item4, Amount = item.Item4, CreatedAt = DateTime.UtcNow });
            }
            db.Payments.Add(new Payment { Id = Guid.NewGuid(), InvoiceId = depositId, ClaimedAmount = 1000000,
                VerifiedAmount = 1000000, PaymentMethod = PaymentMethod.Cash, Status = PaymentStatus.Success,
                PaidAt = DateTime.UtcNow, VerifiedAt = DateTime.UtcNow, VerifiedByUserId = ownerId,
                SubmittedByUserId = ownerId, IdempotencyKey = "final-finance-deposit", CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        var applyPath = $"/api/properties/{propertyId}/contracts/{contractId}/apply-deposit";
        var overviewPath = $"/api/properties/{propertyId}/contracts/{contractId}/settlement-overview";
        var overview = await client.GetFromJsonAsync<JsonElement>(overviewPath);
        Assert.Contains(overview.GetProperty("data").GetProperty("allowedActions").EnumerateArray(), x => x.GetString() == "ApplyDeposit");
        Task<HttpResponseMessage> Apply(Guid invoiceId, decimal amount, string key) => client.PostAsJsonAsync(applyPath,
            new { invoiceId, amount, reason = "Cấn cọc cuối hợp đồng", idempotencyKey = key });
        var tooMuchDeposit = await Apply(monthlyId, 1000001, "deposit-over");
        Assert.Equal(HttpStatusCode.Conflict, tooMuchDeposit.StatusCode);
        Assert.Equal("DEPOSIT_AMOUNT_EXCEEDS_BALANCE", (await tooMuchDeposit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await Apply(damageId, 500001, "debt-over")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Apply(depositId, 1, "deposit-not-target")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Apply(monthlyId, -1, "negative")).StatusCode);
        var applied = await Apply(damageId, 300000, "valid-application");
        Assert.Equal(HttpStatusCode.Created, applied.StatusCode);
        var appliedId = (await applied.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetGuid();
        var retry = await Apply(damageId, 300000, "valid-application");
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        Assert.Equal(appliedId, (await retry.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetGuid());
        var adjPath = $"/api/invoices/{monthlyId}/adjustments";
        var discount = new { type = "Discount", amount = 100000, reason = "Giảm tiền phòng", idempotencyKey = "final-discount", limitToRemainingAmount = true };
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(adjPath, discount)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(adjPath, discount)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(adjPath, new { type = "AdditionalCharge", amount = 200000, reason = "Phụ thu sửa cửa", idempotencyKey = "final-extra" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(adjPath, new {
            type = "Discount", amount = 2100001, reason = "Quá nợ", idempotencyKey = "final-discount-over", limitToRemainingAmount = true })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(adjPath, new {
            type = "AdditionalCharge", amount = 100, reason = "", idempotencyKey = "no-reason" })).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RoomManagementDbContext>();
            Assert.Equal(700000, (await db.ContractDepositBalances.SingleAsync(x => x.ContractId == contractId)).DepositHeld);
            Assert.Equal(200000, (await db.InvoiceBalances.SingleAsync(x => x.InvoiceId == damageId)).RemainingAmount);
            Assert.Equal(2100000, (await db.InvoiceBalances.SingleAsync(x => x.InvoiceId == monthlyId)).RemainingAmount);
            Assert.Equal(2, await db.InvoiceAdjustments.CountAsync(x => x.InvoiceId == monthlyId));
            Assert.Single(await db.DepositApplications.Where(x => x.ContractId == contractId).ToListAsync());
        }
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<HttpResponseMessage> ConcurrentApply(Guid invoiceId, decimal amount, string key) { await gate.Task; return await Apply(invoiceId, amount, key); }
        var a = ConcurrentApply(monthlyId, 600000, "race-monthly");
        var b = ConcurrentApply(damageId, 200000, "race-damage");
        gate.SetResult();
        var pair = await Task.WhenAll(a, b);
        Assert.Single(pair.Where(x => x.StatusCode == HttpStatusCode.Created));
        Assert.Single(pair.Where(x => x.StatusCode == HttpStatusCode.Conflict));
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RoomManagementDbContext>();
            var used = await db.DepositApplications.Where(x => x.ContractId == contractId).SumAsync(x => x.Amount);
            var held = (await db.ContractDepositBalances.SingleAsync(x => x.ContractId == contractId)).DepositHeld;
            Assert.Equal(1000000m - used, held);
            Assert.True(held >= 0);
            Assert.Equal(2, await db.DepositApplications.CountAsync(x => x.ContractId == contractId));
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
