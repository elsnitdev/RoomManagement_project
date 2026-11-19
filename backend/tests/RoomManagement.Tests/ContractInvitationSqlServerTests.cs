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

public sealed class ContractInvitationSqlServerTests
{
    [SqlServerFact]
    public Task Invitation_without_rules_or_file_confirms_members_independently_and_waits_for_start_date() =>
        RunInvitationScenarioAsync(2);

    [SqlServerFact]
    public Task Representative_confirmation_activates_immediately_and_counts_only_accepted_members() =>
        RunInvitationScenarioAsync(0);

    [SqlServerFact]
    public Task Resent_invitation_rejection_blocks_timeout_and_silence_requires_operator_approval() =>
        RunInvitationScenarioAsync(2, testResend: true);

    private static async Task RunInvitationScenarioAsync(int startOffsetDays, bool testResend = false)
    {
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
        var start = RoomManagement.Business.Services.VietnamDate.Today.AddDays(startOffsetDays);
        var end = new DateOnly(start.Year, start.Month, 1).AddMonths(2).AddDays(-1);
        var create = await client.PostAsJsonAsync($"/api/properties/{propertyId}/contracts", new
        {
            roomId, contractCode = "INV-A101", startDate = start, endDate = end,
            monthlyRent = 3000000, depositAmount = 0, tenancyMode = "CoTenancy"
        });
        Assert.True(create.StatusCode == HttpStatusCode.Created,
            $"Create contract: {create.StatusCode} {await create.Content.ReadAsStringAsync()}");
        var created = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal("Invitation", created.GetProperty("workflow").GetString());
        var contractId = created.GetProperty("id").GetGuid();
        for (var index = 0; index < tenantIds.Length; index++)
        {
            var add = await client.PostAsJsonAsync($"/api/properties/{propertyId}/contracts/{contractId}/tenants/by-user", new
            {
                userId = tenantIds[index], joinDate = start, isContactPerson = index == 0,
                role = "ContractParty"
            });
            Assert.Equal(HttpStatusCode.Created, add.StatusCode);
        }
        Guid? signedDocumentId = null;
        var signedPdf = "%PDF-signed-contract"u8.ToArray();
        if (startOffsetDays == 0)
        {
            using var uploadBody = new MultipartFormDataContent();
            uploadBody.Add(new ByteArrayContent(signedPdf), "file", "signed.pdf");
            var upload = await client.PostAsync(
                $"/api/properties/{propertyId}/contracts/{contractId}/documents", uploadBody);
            Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
            signedDocumentId = (await upload.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("data").GetProperty("id").GetGuid();
        }
        var sent = await client.PostAsync($"/api/properties/{propertyId}/contracts/{contractId}/send-offer", null);
        Assert.Equal(HttpStatusCode.OK, sent.StatusCode);
        var offer = (await sent.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal("PendingTenantConfirmation", offer.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, offer.GetProperty("offerRulesVersionId").ValueKind);
        var version = offer.GetProperty("offerVersion").GetInt32();
        var hash = offer.GetProperty("offerTermsHash").GetString()!;
        var ownerDocuments = await client.GetFromJsonAsync<JsonElement>(
            $"/api/properties/{propertyId}/contracts/{contractId}/documents");
        Assert.Equal(signedDocumentId is null ? 0 : 1,
            ownerDocuments.GetProperty("data").GetArrayLength());
        if (existingRulesId is { } rulesId)
        {
            var rulesChanged = await client.PutAsJsonAsync($"/api/properties/{propertyId}/rules", new
            {
                content = "Nội quy mới", expectedVersionId = rulesId
            });
            Assert.Equal(HttpStatusCode.OK, rulesChanged.StatusCode);
        }

        if (testResend)
        {
            var path = $"/api/properties/{propertyId}/contracts/{contractId}";
            var updated = await client.PutAsJsonAsync(path, new
            {
                startDate = start, endDate = end, monthlyRent = 3100000, depositAmount = 0,
                tenancyMode = "CoTenancy"
            });
            Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
            Assert.Equal("Draft", (await updated.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("data").GetProperty("status").GetString());

            var resend = await client.PostAsync(path + "/send-offer", null);
            Assert.Equal(HttpStatusCode.OK, resend.StatusCode);
            var revised = (await resend.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
            var revisedVersion = revised.GetProperty("offerVersion").GetInt32();
            var deadline = revised.GetProperty("offerResponseDeadline").GetDateTime();
            Assert.Equal(version + 1, revisedVersion);
            Assert.InRange(deadline, DateTime.UtcNow.AddDays(3).AddMinutes(-1), DateTime.UtcNow.AddDays(3));

            await SignInAsync(client, tenantAccounts[0].Phone, "Password123!");
            var rejectPath = $"/api/me/contracts/{contractId}/reject";
            var staleReject = await client.PostAsJsonAsync(rejectPath, new { offerVersion = version });
            Assert.Equal(HttpStatusCode.Conflict, staleReject.StatusCode);
            var rejection = await client.PostAsJsonAsync(rejectPath, new { offerVersion = revisedVersion, reason = "Cần trao đổi lại" });
            Assert.Equal(HttpStatusCode.OK, rejection.StatusCode);

            using (var scope = factory.Services.CreateScope())
            {
                var store = scope.ServiceProvider.GetRequiredService<IContractStore>();
                var db = scope.ServiceProvider.GetRequiredService<RoomManagementDbContext>();
                await store.ExpireOfferResponsesAsync(deadline, CancellationToken.None);
                var rejected = await db.Contracts.AsNoTracking().SingleAsync(x => x.Id == contractId);
                Assert.Equal(ContractStatus.Draft, rejected.Status);
                Assert.Equal("Cần trao đổi lại", rejected.TenantRejectionReason);
                Assert.Null(rejected.OfferResponseTimedOutAt);
            }

            await SignInAsync(client, "invitationOwner", "Pass123!");
            var unchanged = await client.PostAsync(path + "/send-offer", null);
            Assert.Equal(HttpStatusCode.Conflict, unchanged.StatusCode);
            var revisedAgain = await client.PutAsJsonAsync(path, new
            {
                startDate = start, endDate = end, monthlyRent = 3050000, depositAmount = 0,
                tenancyMode = "CoTenancy"
            });
            Assert.Equal(HttpStatusCode.OK, revisedAgain.StatusCode);
            var sentAgain = await client.PostAsync(path + "/send-offer", null);
            Assert.Equal(HttpStatusCode.OK, sentAgain.StatusCode);
            var current = (await sentAgain.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
            var nextDeadline = current.GetProperty("offerResponseDeadline").GetDateTime();
            var nextVersion = current.GetProperty("offerVersion").GetInt32();
            Assert.Equal(revisedVersion + 1, nextVersion);
            using (var scope = factory.Services.CreateScope())
            {
                var store = scope.ServiceProvider.GetRequiredService<IContractStore>();
                var db = scope.ServiceProvider.GetRequiredService<RoomManagementDbContext>();
                await store.ExpireOfferResponsesAsync(nextDeadline.AddTicks(-1), CancellationToken.None);
                Assert.Equal(ContractStatus.PendingTenantConfirmation,
                    await db.Contracts.Where(x => x.Id == contractId).Select(x => x.Status).SingleAsync());
                await store.ExpireOfferResponsesAsync(nextDeadline, CancellationToken.None);
                await store.ExpireOfferResponsesAsync(nextDeadline.AddMinutes(1), CancellationToken.None);
                var timedOut = await db.Contracts.AsNoTracking().SingleAsync(x => x.Id == contractId);
                Assert.Equal(ContractStatus.PendingOperatorConfirmation, timedOut.Status);
                Assert.Null(timedOut.TenantConfirmedAt);
                Assert.NotNull(timedOut.OfferResponseTimedOutAt);
                Assert.Equal(1, await db.ContractOfferEvents.CountAsync(x => x.ContractId == contractId &&
                    x.Action == "TenantResponseTimedOut" && x.OfferVersion == nextVersion));
                var types = await db.UserNotifications.Where(x => x.ContractId == contractId)
                    .Select(x => x.Type).Distinct().ToListAsync();
                foreach (var type in new[] { "ContractOfferResent", "ContractOfferRejected", "ContractOfferTimedOut" })
                {
                    Assert.Contains(type, types);
                    Assert.Contains(type + "Operator", types);
                }
            }
            var approval = await client.PostAsync(path + "/activate", null);
            Assert.Equal(HttpStatusCode.OK, approval.StatusCode);
            Assert.Equal("PendingStart", (await approval.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("data").GetProperty("status").GetString());
            return;
        }

        await SignInAsync(client, tenantAccounts[1].Phone, "Password123!");
        var tenantDocuments = await client.GetFromJsonAsync<JsonElement>($"/api/me/contracts/{contractId}/documents");
        Assert.Equal(signedDocumentId is null ? 0 : 1,
            tenantDocuments.GetProperty("data").GetArrayLength());
        if (signedDocumentId is { } documentId)
            Assert.Equal(signedPdf, await client.GetByteArrayAsync(
                $"/api/me/contracts/{contractId}/documents/{documentId}/content"));
        var memberJoin = await client.PostAsJsonAsync($"/api/me/contracts/{contractId}/join", new
        { offerVersion = version, termsHash = hash });
        Assert.Equal(HttpStatusCode.OK, memberJoin.StatusCode);
        var memberList = await client.GetFromJsonAsync<JsonElement>("/api/me/contracts");
        Assert.Contains(memberList.GetProperty("data").EnumerateArray(), item => item.GetProperty("id").GetGuid() == contractId);

        await SignInAsync(client, tenantAccounts[0].Phone, "Password123!");
        var confirmed = await client.PostAsJsonAsync($"/api/me/contracts/{contractId}/confirm", new
        { offerVersion = version, termsHash = hash });
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        var confirmedData = (await confirmed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal(startOffsetDays == 0 ? "Active" : "PendingStart",
            confirmedData.GetProperty("status").GetString());
        if (existingRulesId is not null)
        {
            var myRules = await client.GetFromJsonAsync<JsonElement>("/api/me/contract-rules");
            Assert.Empty(myRules.GetProperty("data").EnumerateArray());
        }

        await SignInAsync(client, "invitationOwner", "Pass123!");
        var detail = await client.GetFromJsonAsync<JsonElement>(
            $"/api/properties/{propertyId}/contracts/{contractId}");
        var detailData = detail.GetProperty("data");
        Assert.Equal(startOffsetDays == 0 ? 2 : 0,
            detailData.GetProperty("currentOccupantCount").GetInt32());
        Assert.Equal(startOffsetDays == 0 ? 2 : 0,
            detailData.GetProperty("memberCount").GetInt32());
        Assert.All(detailData.GetProperty("tenants").EnumerateArray(), tenant =>
            Assert.Equal(JsonValueKind.Null, tenant.GetProperty("currentRulesVersionId").ValueKind));
        var manualActivate = await client.PostAsync($"/api/properties/{propertyId}/contracts/{contractId}/activate", null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, manualActivate.StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IContractStore>();
            await store.ExpireDueAsync(start, CancellationToken.None);
            var db = scope.ServiceProvider.GetRequiredService<RoomManagementDbContext>();
            var contract = await db.Contracts.AsNoTracking().SingleAsync(x => x.Id == contractId);
            Assert.Equal(ContractStatus.Active, contract.Status);
            var links = await db.ContractTenants.Where(x => x.ContractId == contractId)
                .OrderBy(x => x.TenantId).ToListAsync();
            Assert.Equal(2, links.Count(x => x.InvitationAcceptedOfferVersion == version));
            Assert.Single(links, x => x.InvitationAcceptedOfferVersion == null);
            Assert.Empty(await db.ContractRulesAcceptances.Where(x => links.Select(link => link.Id).Contains(x.ContractTenantId)).ToListAsync());
        }

        if (startOffsetDays == 0)
        {
            var path = $"/api/properties/{propertyId}/contracts/{contractId}";
            var members = detailData.GetProperty("tenants").EnumerateArray().ToList();
            var representativeIndex = members.FindIndex(x => x.GetProperty("isContactPerson").GetBoolean());
            Assert.True(representativeIndex >= 0);
            var representativeId = members[representativeIndex].GetProperty("id").GetGuid();
            var unconfirmed = Assert.Single(members, x => !x.GetProperty("canRepresent").GetBoolean());
            var leavePath = $"{path}/tenants/{representativeId}/leave";
            using var invalid = await client.PostAsJsonAsync(leavePath, new
            {
                leaveDate = start, replacementLinkId = unconfirmed.GetProperty("id").GetGuid()
            });
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            await AssertRepresentativeAsync(representativeId);

            await SignInAsync(client, tenantAccounts[2].Phone, "Password123!");
            using var join = await client.PostAsJsonAsync($"/api/me/contracts/{contractId}/join", new
            { offerVersion = version, termsHash = hash });
            Assert.Equal(HttpStatusCode.OK, join.StatusCode);
            await SignInAsync(client, "invitationOwner", "Pass123!");

            var nextId = members[(representativeIndex + 1) % members.Count].GetProperty("id").GetGuid();
            var selectedId = members[(representativeIndex + 2) % members.Count].GetProperty("id").GetGuid();
            using var selected = await client.PostAsJsonAsync(leavePath, new { leaveDate = start, replacementLinkId = selectedId });
            Assert.Equal(HttpStatusCode.OK, selected.StatusCode);
            await AssertRepresentativeAsync(selectedId);

            using var automatic = await client.PostAsJsonAsync($"{path}/tenants/{selectedId}/leave", new { leaveDate = start });
            Assert.Equal(HttpStatusCode.OK, automatic.StatusCode);
            await AssertRepresentativeAsync(nextId);

            async Task AssertRepresentativeAsync(Guid expectedId)
            {
                var updated = await client.GetFromJsonAsync<JsonElement>(path);
                var tenants = updated.GetProperty("data").GetProperty("tenants").EnumerateArray();
                var representative = Assert.Single(tenants, x => x.GetProperty("isContactPerson").GetBoolean());
                Assert.Equal(expectedId, representative.GetProperty("id").GetGuid());
                Assert.Equal(JsonValueKind.Null, representative.GetProperty("leaveDate").ValueKind);
            }
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
