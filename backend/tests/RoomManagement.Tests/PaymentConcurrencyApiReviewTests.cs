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

public sealed class PaymentConcurrencyApiReviewTests
{
    [SqlServerFact]
    public async Task Duplicate_and_concurrent_reviews_capture_api_and_sql()
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




        var calls = new ConcurrentQueue<object>();
        var snapshots = new List<object>();
        async Task<(int Status, JsonElement Output)> Call(string id, string path, object input, AuthenticationHeaderValue auth)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path);
            request.Headers.Authorization = auth;
            request.Content = JsonContent.Create(input);
            var started = DateTimeOffset.UtcNow;
            using var response = await client.SendAsync(request);
            var raw = await response.Content.ReadAsStringAsync();
            JsonElement output;
            try { output = JsonDocument.Parse(raw).RootElement.Clone(); }
            catch { output = JsonSerializer.SerializeToElement(raw); }
            calls.Enqueue(new { id, method = "POST", path, input, started, finished = DateTimeOffset.UtcNow,
                http = (int)response.StatusCode, output });
            return ((int)response.StatusCode, output);
        }
        await SignInAsync(client, "invitationOwner", "Pass123!");
        var ownerAuth = client.DefaultRequestHeaders.Authorization!;
        await SignInAsync(client, tenantAccounts[0].Phone, "Password123!");
        var tenantA = client.DefaultRequestHeaders.Authorization!;
        await SignInAsync(client, tenantAccounts[1].Phone, "Password123!");
        var tenantB = client.DefaultRequestHeaders.Authorization!;
        var today = RoomManagement.Business.Services.VietnamDate.Today;
        async Task<Guid> Invoice(string code)
        {
            var created = await Call(code + "-SETUP", $"/api/properties/{propertyId}/contracts", new {
                roomId, contractCode = code, startDate = today, endDate = new DateOnly(today.Year, today.Month, 1).AddMonths(2).AddDays(-1),
                monthlyRent = 3000000, depositAmount = 1000000, paymentDueDay = 5,
                members = new[] { new { userId = tenantIds[0], isContactPerson = true }, new { userId = tenantIds[1], isContactPerson = false } }
            }, ownerAuth);
            Assert.Equal(201, created.Status);
            return created.Output.GetProperty("data").GetProperty("depositInvoiceId").GetGuid();
        }
        object Claim(decimal amount, Guid key) => new { amount, method = "Cash", paidAt = new DateTimeOffset(DateTime.UtcNow.AddMinutes(-5).Ticks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond, TimeSpan.Zero),
            transactionCode = (string?)null, note = "API concurrency review", idempotencyKey = key };
        async Task<(decimal Paid, decimal Remaining, int Success, int Payments, int Reviews)> Snapshot(string label, Guid invoiceId)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<RoomManagementDbContext>();
            var payments = await db.Payments.AsNoTracking().Where(x => x.InvoiceId == invoiceId).ToListAsync();
            var paymentIds = payments.Select(x => x.Id).ToArray();
            var reviews = await db.PaymentReviewEvents.AsNoTracking().Where(x => paymentIds.Contains(x.PaymentId)).ToListAsync();
            var balance = await db.InvoiceBalances.AsNoTracking().SingleAsync(x => x.InvoiceId == invoiceId);
            snapshots.Add(new { label, invoiceId, balance.EffectiveAmount, balance.TotalPaid, balance.RemainingAmount,
                payments = payments.Select(x => new { x.Id, x.Status, x.ClaimedAmount, x.VerifiedAmount, x.SubmittedByUserId, x.IdempotencyKey }),
                reviews = reviews.Select(x => new { x.Id, x.PaymentId, x.PreviousStatus, x.NewStatus, x.NewVerifiedAmount }) });
            return (balance.TotalPaid, balance.RemainingAmount, payments.Count(x => x.Status == PaymentStatus.Success), payments.Count, reviews.Count);
        }
        async Task<T[]> Parallel<T>(Func<Task<T>> first, Func<Task<T>> second)
        {
            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task<T> Start(Func<Task<T>> action) { await ready.Task; return await action(); }
            var a = Start(first); var b = Start(second); ready.SetResult();
            return await Task.WhenAll(a, b);
        }
        var duplicateInvoice = await Invoice("TT05-REVIEW");
        var payload = Claim(500000, Guid.NewGuid());
        var declarePath = $"/api/me/invoices/{duplicateInvoice}/payments";
        var declarations = await Parallel(() => Call("TT05-DECLARE-A", declarePath, payload, tenantA),
            () => Call("TT05-DECLARE-B", declarePath, payload, tenantA));
        var firstPayment = declarations.First(x => x.Status == 201).Output.GetProperty("data").GetProperty("id").GetGuid();
        var retry = await Call("TT05-DECLARE-RETRY", declarePath, payload, tenantA);
        var pending = await Snapshot("TT05-after-duplicate-declare", duplicateInvoice);
        var review = new { status = "Success", verifiedAmount = 500000, reason = "Đã nhận đủ tiền" };
        var reviewPath = $"/api/payments/{firstPayment}/review";
        var confirmations = await Parallel(() => Call("TT05-REVIEW-A", reviewPath, review, ownerAuth),
            () => Call("TT05-REVIEW-B", reviewPath, review, ownerAuth));
        await Call("TT05-REVIEW-RETRY", reviewPath, review, ownerAuth);
        var confirmed = await Snapshot("TT05-after-duplicate-confirm", duplicateInvoice);
        var receiptKey = Guid.NewGuid();
        var receipt = new { amount = 100000, method = "Cash", idempotencyKey = receiptKey };
        await Call("TT05-RECEIVED-A", $"/api/invoices/{duplicateInvoice}/payments/received", receipt, ownerAuth);
        await Call("TT05-RECEIVED-RETRY", $"/api/invoices/{duplicateInvoice}/payments/received", receipt, ownerAuth);
        var received = await Snapshot("TT05-after-duplicate-ownerAuth-receipt", duplicateInvoice);
        var tt05 = pending.Payments == 1 && pending.Paid == 0 && confirmed.Paid == 500000 && confirmed.Reviews == 1 &&
            confirmed.Success == 1 && received.Paid == 600000 && received.Payments == 2 && received.Remaining == 400000 &&
            retry.Output.GetProperty("data").GetProperty("id").GetGuid() == firstPayment && confirmations.All(x => x.Status == 200);
        var concurrentRounds = new List<object>();
        var tt06 = true;
        for (var round = 1; round <= 5; round++)
        {
            var invoiceId = await Invoice("TT06-" + round);
            var path = $"/api/me/invoices/{invoiceId}/payments";
            var a = await Call($"TT06-{round}-DECLARE-A", path, Claim(700000, Guid.NewGuid()), tenantA);
            var b = await Call($"TT06-{round}-DECLARE-B", path, Claim(700000, Guid.NewGuid()), tenantB);
            Assert.Equal(201, a.Status); Assert.Equal(201, b.Status);
            var aId = a.Output.GetProperty("data").GetProperty("id").GetGuid();
            var bId = b.Output.GetProperty("data").GetProperty("id").GetGuid();
            var successPayload = new { status = "Success", verifiedAmount = 700000, reason = "Đối soát giao dịch" };
            var pair = await Parallel(() => Call($"TT06-{round}-CONFIRM-A", $"/api/payments/{aId}/review", successPayload, ownerAuth),
                () => Call($"TT06-{round}-CONFIRM-B", $"/api/payments/{bId}/review", successPayload, ownerAuth));
            var state = await Snapshot($"TT06-round-{round}", invoiceId);
            var passed = pair.Count(x => x.Status == 200) == 1 && pair.Count(x => x.Status is 400 or 409 or 422) == 1 &&
                state.Success == 1 && state.Paid == 700000 && state.Remaining == 300000;
            tt06 &= passed;
            concurrentRounds.Add(new { round, http = pair.Select(x => x.Status).ToArray(), state.Paid, state.Remaining, state.Success, passed });
        }
        var report = new { ranAt = DateTimeOffset.UtcNow, environment = "ASP.NET Core TestServer + isolated SQL LocalDB; hosted services disabled",
            propertyId, roomId, TT05 = tt05, TT06 = tt06, concurrentRounds, calls = calls.ToArray(), snapshots };
        var reportPath = Environment.GetEnvironmentVariable("PAYMENT_REVIEW_OUTPUT");
        if (!string.IsNullOrWhiteSpace(reportPath))
            await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report,
                new JsonSerializerOptions { WriteIndented = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } }));
        Assert.True(tt05 && tt06, $"TT05={tt05}; TT06={tt06}.");
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
