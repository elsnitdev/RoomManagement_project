using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RoomManagement.Data;
using RoomManagement.Domain;
using RoomManagement.Domain.Constants;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;
using ServiceEntity = RoomManagement.Domain.Entities.Service;

// Run from the repository root. All accounts and the property belong to this fixture.
var command = args.SingleOrDefault()?.ToLowerInvariant();
if (command is not ("seed" or "reset" or "status"))
{
    Console.Error.WriteLine("Usage: dotnet run --project backend/tools/DemoDataTool -- seed|status|reset");
    return 2;
}

var configPath = Path.GetFullPath("backend/src/RoomManagement.API/appsettings.Local.json");
if (!File.Exists(configPath))
    throw new InvalidOperationException($"Local configuration not found: {configPath}");
using var config = JsonDocument.Parse(await File.ReadAllTextAsync(configPath));
var connection = config.RootElement.GetProperty("ConnectionStrings")
    .GetProperty("RoomManagement").GetString()
    ?? throw new InvalidOperationException("Missing RoomManagement connection string.");
var options = new DbContextOptionsBuilder<RoomManagementDbContext>().UseSqlServer(connection).Options;
await using var db = new RoomManagementDbContext(options);
var databaseName = db.Database.GetDbConnection().Database;
if (!string.Equals(databaseName, "RoomManagement", StringComparison.OrdinalIgnoreCase)
    && !databaseName.StartsWith("RoomManagement_", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("Demo tool only accepts a RoomManagement database.");
if (!(await db.Database.GetPendingMigrationsAsync()).Any())
    Console.WriteLine($"Database: {databaseName}");
else
    throw new InvalidOperationException("Apply pending migrations before using the demo tool.");

const string prefix = "rm_demo_";
const string adminId = "rm-demo-admin";
const string landlordId = "rm-demo-landlord";
const string pendingLandlordId = "rm-demo-landlord-pending";
const string tenantAId = "rm-demo-tenant-a";
const string tenantBId = "rm-demo-tenant-b";
const string tenantCId = "rm-demo-tenant-c";
const string tenantDId = "rm-demo-tenant-d";
var demoUserIds = new[] { adminId, landlordId, pendingLandlordId, tenantAId, tenantBId, tenantCId, tenantDId };
var propertyId = Guid.Parse("d0000000-0000-4000-8000-000000000001");
var rooms = Enumerable.Range(1, 4).Select(i => Guid.Parse($"d0000000-0000-4000-8000-{i + 10:D12}")).ToArray();
var contracts = Enumerable.Range(1, 3).Select(i => Guid.Parse($"d0000000-0000-4000-8000-{i + 20:D12}")).ToArray();
var profiles = Enumerable.Range(1, 4).Select(i => Guid.Parse($"d0000000-0000-4000-8000-{i + 30:D12}")).ToArray();

if (command == "status")
{
    var count = await db.Users.CountAsync(u => demoUserIds.Contains(u.Id));
    var propertyExists = await db.Properties.AnyAsync(p => p.Id == propertyId);
    Console.WriteLine($"Demo accounts: {count}/{demoUserIds.Length}; demo property: {(propertyExists ? "present" : "absent")}");
    if (propertyExists)
    {
        Console.WriteLine($"PropertyId: {propertyId}");
        var demoRooms = db.Rooms.Where(r => r.PropertyId == propertyId).Select(r => r.Id);
        var demoContracts = db.Contracts.Where(c => demoRooms.Contains(c.RoomId)).Select(c => c.Id);
        var demoInvoices = db.Invoices.Where(i => demoContracts.Contains(i.ContractId)).Select(i => i.Id);
        Console.WriteLine($"Rooms: {await demoRooms.CountAsync()}; contracts: {await demoContracts.CountAsync()}; invoices: {await demoInvoices.CountAsync()}; payments: {await db.Payments.CountAsync(p => demoInvoices.Contains(p.InvoiceId))}");
    }
    return 0;
}

if (command == "seed")
{
    if (await db.Users.AnyAsync(u => demoUserIds.Contains(u.Id) || u.UserName.StartsWith(prefix))
        || await db.Properties.AnyAsync(p => p.Id == propertyId))
        throw new InvalidOperationException("Demo data already exists. Run status or reset first.");
    var passwordPath = Path.GetFullPath("backend/tools/DemoDataTool/.demo-password");
    var password = Environment.GetEnvironmentVariable("ROOMMANAGEMENT_DEMO_PASSWORD")
        ?? (File.Exists(passwordPath) ? (await File.ReadAllTextAsync(passwordPath)).Trim()
            : Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)) + "!a1");
    if (string.IsNullOrWhiteSpace(password) || password.Length < 12)
        throw new InvalidOperationException("Demo password must contain at least 12 characters.");
    await File.WriteAllTextAsync(passwordPath, password);
    await using var tx = await db.Database.BeginTransactionAsync();
    var now = DateTime.UtcNow;
    var localToday = DateOnly.FromDateTime(now.AddHours(7));
    var first = new DateOnly(localToday.Year, localToday.Month, 1);
    var previousMonth = first.AddMonths(-1);
    var hasher = new PasswordHasher<AppUser>();
    AppUser User(string id, string name, string role, string fullName, bool approved = true)
    {
        var user = new AppUser
        {
            Id = id, UserName = prefix + name, NormalizedUserName = (prefix + name).ToUpperInvariant(),
            FullName = fullName, Role = role, IsActive = approved,
            ApprovalStatus = approved ? ApprovalStatuses.Approved : ApprovalStatuses.Pending,
            ReviewedAt = approved ? now : null, CreatedAt = now
        };
        user.PasswordHash = hasher.HashPassword(user, password);
        return user;
    }
    db.Users.AddRange(
        User(adminId, "admin", UserRoles.Admin, "Quản trị dữ liệu mẫu"),
        User(landlordId, "landlord", UserRoles.Landlord, "Chủ trọ dữ liệu mẫu"),
        User(pendingLandlordId, "landlord_pending", UserRoles.Landlord, "Chủ trọ chờ duyệt", false),
        User(tenantAId, "tenant_a", UserRoles.Tenant, "Nguyễn An"),
        User(tenantBId, "tenant_b", UserRoles.Tenant, "Trần Bình"),
        User(tenantCId, "tenant_c", UserRoles.Tenant, "Lê Chi"),
        User(tenantDId, "tenant_d", UserRoles.Tenant, "Phạm Dũng"));
    db.Properties.Add(new Property
    {
        Id = propertyId, OwnerUserId = landlordId, Name = "RM DEMO - Khu trọ kiểm thử",
        Address = "Dữ liệu mẫu, không phải địa chỉ thật", IsActive = true, CreatedAt = now
    });
    db.Rooms.AddRange(new[]
    {
        new Room { Id = rooms[0], PropertyId = propertyId, RoomNumber = "DEMO-A", BasePrice = 3_000_000, MaxOccupants = 2, Status = RoomStatus.Occupied, CreatedAt = now },
        new Room { Id = rooms[1], PropertyId = propertyId, RoomNumber = "DEMO-B", BasePrice = 2_500_000, MaxOccupants = 2, Status = RoomStatus.Occupied, CreatedAt = now },
        new Room { Id = rooms[2], PropertyId = propertyId, RoomNumber = "DEMO-C", BasePrice = 2_800_000, MaxOccupants = 2, Status = RoomStatus.Available, CreatedAt = now },
        new Room { Id = rooms[3], PropertyId = propertyId, RoomNumber = "DEMO-D", BasePrice = 2_200_000, MaxOccupants = 2, Status = RoomStatus.Occupied, CreatedAt = now }
    });
    var tenantIds = new[] { tenantAId, tenantBId, tenantCId, tenantDId };
    var tenantNames = new[] { "Nguyễn An", "Trần Bình", "Lê Chi", "Phạm Dũng" };
    for (var i = 0; i < profiles.Length; i++)
        db.TenantProfiles.Add(new TenantProfile
        {
            Id = profiles[i], UserId = tenantIds[i], CreatedByUserId = landlordId,
            FullName = tenantNames[i], PhoneNumber = $"09000001{i + 1:D2}", CreatedAt = now
        });
    for (var i = 0; i < contracts.Length; i++)
    {
        var roomIndex = i == 2 ? 3 : i;
        db.Contracts.Add(new Contract
        {
            Id = contracts[i], ContractCode = $"RM-DEMO-{(char)('A' + roomIndex)}", RoomId = rooms[roomIndex],
            StartDate = previousMonth, MonthlyRent = new[] { 3_000_000m, 2_500_000m, 2_200_000m }[i],
            DepositAmount = i == 0 ? 1_000_000 : 0, PaymentDueDay = 5,
            Status = ContractStatus.Active, CreatedAt = now
        });
        db.ContractTenants.Add(new ContractTenant
        {
            Id = Guid.NewGuid(), ContractId = contracts[i], TenantId = profiles[roomIndex],
            JoinDate = previousMonth, IsContactPerson = true, CreatedAt = now
        });
    }
    foreach (var type in new[] { UtilityType.Electricity, UtilityType.Water })
        db.UtilityRates.Add(new UtilityRate
        {
            Id = Guid.NewGuid(), PropertyId = propertyId, UtilityType = type,
            UnitPrice = type == UtilityType.Electricity ? 3_500 : 15_000,
            Unit = type == UtilityType.Electricity ? "kWh" : "m³",
            EffectiveFrom = previousMonth, CreatedAt = now
        });
    for (var i = 0; i < 2; i++)
    {
        db.MeterReadings.Add(new MeterReading { Id = Guid.NewGuid(), RoomId = rooms[i], ContractId = contracts[i], MeterType = UtilityType.Electricity, Year = (short)previousMonth.Year, Month = (byte)previousMonth.Month, PreviousValue = 100, CurrentValue = i == 0 ? 180 : 150, RecordedAt = now });
        db.MeterReadings.Add(new MeterReading { Id = Guid.NewGuid(), RoomId = rooms[i], ContractId = contracts[i], MeterType = UtilityType.Water, Year = (short)previousMonth.Year, Month = (byte)previousMonth.Month, PreviousValue = 20, CurrentValue = i == 0 ? 32 : 28, RecordedAt = now });
    }
    var internetId = Guid.NewGuid();
    var parkingId = Guid.NewGuid();
    db.Services.AddRange(
        new ServiceEntity { Id = internetId, PropertyId = propertyId, Name = "Internet DEMO", Unit = "tháng", DefaultPrice = 100_000, CreatedAt = now },
        new ServiceEntity { Id = parkingId, PropertyId = propertyId, Name = "Giữ xe DEMO", Unit = "xe/tháng", DefaultPrice = 80_000, CreatedAt = now });
    db.RoomServices.AddRange(
        new RoomService { Id = Guid.NewGuid(), RoomId = rooms[0], ServiceId = internetId, StartDate = previousMonth, CreatedAt = now },
        new RoomService { Id = Guid.NewGuid(), RoomId = rooms[1], ServiceId = parkingId, StartDate = previousMonth, CreatedAt = now });
    // One issued unpaid invoice and one paid invoice let both parties inspect payment states.
    var invoiceA = Guid.NewGuid();
    var invoiceB = Guid.NewGuid();
    var issued = previousMonth.AddMonths(1);
    db.Invoices.AddRange(
        new Invoice { Id = invoiceA, ContractId = contracts[0], RoomId = rooms[0], InvoiceCode = $"RM-DEMO-A-{previousMonth:yyyyMM}", InvoiceType = InvoiceType.Monthly, BillingYear = (short)previousMonth.Year, BillingMonth = (byte)previousMonth.Month, IssueDate = issued, DueDate = issued.AddDays(4), TotalAmount = 3_560_000, Status = InvoiceStatus.Issued, CreatedAt = now },
        new Invoice { Id = invoiceB, ContractId = contracts[1], RoomId = rooms[1], InvoiceCode = $"RM-DEMO-B-{previousMonth:yyyyMM}", InvoiceType = InvoiceType.Monthly, BillingYear = (short)previousMonth.Year, BillingMonth = (byte)previousMonth.Month, IssueDate = issued, DueDate = issued.AddDays(4), TotalAmount = 2_875_000, Status = InvoiceStatus.Issued, CreatedAt = now });
    void Item(Guid invoiceId, InvoiceItemType type, string description, decimal quantity, decimal price) =>
        db.InvoiceItems.Add(new InvoiceItem { Id = Guid.NewGuid(), InvoiceId = invoiceId, ItemType = type, Description = description, Quantity = quantity, UnitPrice = price, Amount = quantity * price, CreatedAt = now });
    Item(invoiceA, InvoiceItemType.Rent, "Tiền phòng DEMO-A", 1, 3_000_000);
    Item(invoiceA, InvoiceItemType.Electricity, "Điện", 80, 3_500);
    Item(invoiceA, InvoiceItemType.Water, "Nước", 12, 15_000);
    Item(invoiceA, InvoiceItemType.Service, "Internet", 1, 100_000);
    Item(invoiceB, InvoiceItemType.Rent, "Tiền phòng DEMO-B", 1, 2_500_000);
    Item(invoiceB, InvoiceItemType.Electricity, "Điện", 50, 3_500);
    Item(invoiceB, InvoiceItemType.Water, "Nước", 8, 15_000);
    Item(invoiceB, InvoiceItemType.Service, "Giữ xe", 1, 80_000);
    db.Payments.Add(new Payment
    {
        Id = Guid.NewGuid(), InvoiceId = invoiceB, SubmittedByUserId = tenantBId,
        ClaimedAmount = 2_875_000, VerifiedAmount = 2_875_000, PaymentMethod = PaymentMethod.BankTransfer,
        Status = PaymentStatus.Success, PaidAt = now, VerifiedAt = now, VerifiedByUserId = landlordId,
        IdempotencyKey = $"rm-demo-payment-{previousMonth:yyyyMM}", CreatedAt = now
    });
    await db.SaveChangesAsync();
    await tx.CommitAsync();
    Console.WriteLine("Created reusable fixture. Usernames: rm_demo_admin, rm_demo_landlord, rm_demo_landlord_pending, rm_demo_tenant_a/b/c/d");
    Console.WriteLine($"Local password file: {passwordPath}");
    Console.WriteLine($"PropertyId: {propertyId}; invoice period: {previousMonth:yyyy-MM}");
    Console.WriteLine("DEMO-A: active + unpaid; DEMO-B: active + paid; DEMO-C: empty; DEMO-D: active, no invoice.");
    return 0;
}

// Reset is restricted to the demo landlord's properties and profiles plus fixed demo accounts.
await using (var tx = await db.Database.BeginTransactionAsync())
{
    var knownDemoUsers = await db.Users.Where(u => demoUserIds.Contains(u.Id)).ToListAsync();
    if (knownDemoUsers.Any(u => !u.UserName.StartsWith(prefix, StringComparison.Ordinal)))
        throw new InvalidOperationException("A reserved demo user ID belongs to a non-demo account. Reset aborted.");
    var propertyIds = db.Properties.Where(p => p.OwnerUserId == landlordId || p.OwnerUserId == pendingLandlordId).Select(p => p.Id);
    var roomIds = db.Rooms.Where(r => propertyIds.Contains(r.PropertyId)).Select(r => r.Id);
    var contractIds = db.Contracts.Where(c => roomIds.Contains(c.RoomId)).Select(c => c.Id);
    var invoiceIds = db.Invoices.Where(i => contractIds.Contains(i.ContractId)).Select(i => i.Id);
    var paymentIds = db.Payments.Where(p => invoiceIds.Contains(p.InvoiceId)).Select(p => p.Id);
    var tenantProfileIds = db.TenantProfiles.Where(t => t.CreatedByUserId == landlordId || t.CreatedByUserId == pendingLandlordId || (t.UserId != null && demoUserIds.Contains(t.UserId))).Select(t => t.Id);
    var contractTenantIds = db.ContractTenants.Where(t => contractIds.Contains(t.ContractId)).Select(t => t.Id);
    var rulesIds = db.ContractRulesVersions.Where(r => (r.PropertyId != null && propertyIds.Contains(r.PropertyId.Value)) || (r.ContractId != null && contractIds.Contains(r.ContractId.Value))).Select(r => r.Id);
    var acceptanceIds = db.ContractRulesAcceptances.Where(a => rulesIds.Contains(a.RulesVersionId) || contractTenantIds.Contains(a.ContractTenantId)).Select(a => a.Id);

    var blobNames = await db.RoomImages.Where(x => roomIds.Contains(x.RoomId)).Select(x => x.BlobName)
        .Concat(db.PaymentEvidence.Where(x => paymentIds.Contains(x.PaymentId)).Select(x => x.BlobName))
        .Concat(db.ContractRulesEvidence.Where(x => acceptanceIds.Contains(x.AcceptanceId)).Select(x => x.BlobName))
        .Distinct().ToListAsync();
    var now = DateTime.UtcNow;
    foreach (var blob in blobNames)
        if (!await db.BlobCleanupJobs.AnyAsync(j => j.BlobName == blob))
            db.BlobCleanupJobs.Add(new BlobCleanupJob { Id = Guid.NewGuid(), BlobName = blob, CreatedAt = now, NextAttemptAt = now });
    await db.SaveChangesAsync();

    await db.ContractRulesEvidence.Where(x => acceptanceIds.Contains(x.AcceptanceId)).ExecuteDeleteAsync();
    await db.ContractRulesAcceptances.Where(x => acceptanceIds.Contains(x.Id)).ExecuteDeleteAsync();
    await db.MemberCommitments.Where(x => contractTenantIds.Contains(x.ContractTenantId) || (x.SignerTenantProfileId != null && tenantProfileIds.Contains(x.SignerTenantProfileId.Value))).ExecuteDeleteAsync();
    await db.TenantRequests.Where(x => contractIds.Contains(x.ContractId)).ExecuteDeleteAsync();
    await db.PaymentEvidence.Where(x => paymentIds.Contains(x.PaymentId)).ExecuteDeleteAsync();
    await db.PaymentReviewEvents.Where(x => paymentIds.Contains(x.PaymentId)).ExecuteDeleteAsync();
    await db.Payments.Where(x => paymentIds.Contains(x.Id)).ExecuteDeleteAsync();
    await db.Refunds.Where(x => invoiceIds.Contains(x.InvoiceId)).ExecuteDeleteAsync();
    await db.DepositApplications.Where(x => contractIds.Contains(x.ContractId)).ExecuteDeleteAsync();
    await db.InvoiceAdjustments.Where(x => invoiceIds.Contains(x.InvoiceId)).ExecuteDeleteAsync();
    await db.InvoiceItems.Where(x => invoiceIds.Contains(x.InvoiceId)).ExecuteDeleteAsync();
    await db.Invoices.Where(x => invoiceIds.Contains(x.Id)).ExecuteDeleteAsync();
    await db.ContractSettlements.Where(x => contractIds.Contains(x.ContractId)).ExecuteDeleteAsync();
    await db.ContractDocuments.Where(x => contractIds.Contains(x.ContractId)).ExecuteDeleteAsync();
    await db.MeterReadings.Where(x => roomIds.Contains(x.RoomId) || (x.ContractId != null && contractIds.Contains(x.ContractId.Value))).ExecuteDeleteAsync();
    await db.ContractTenants.Where(x => contractTenantIds.Contains(x.Id)).ExecuteDeleteAsync();
    await db.ContractRulesVersions.Where(x => rulesIds.Contains(x.Id)).ExecuteDeleteAsync();
    await db.Contracts.Where(x => contractIds.Contains(x.Id)).ExecuteDeleteAsync();
    await db.RoomServices.Where(x => roomIds.Contains(x.RoomId)).ExecuteDeleteAsync();
    await db.RoomImages.Where(x => roomIds.Contains(x.RoomId)).ExecuteDeleteAsync();
    await db.RoomBatchSubmissions.Where(x => propertyIds.Contains(x.PropertyId)).ExecuteDeleteAsync();
    await db.RoomNumberCounters.Where(x => propertyIds.Contains(x.PropertyId)).ExecuteDeleteAsync();
    await db.Rooms.Where(x => roomIds.Contains(x.Id)).ExecuteDeleteAsync();
    await db.RoomRows.Where(x => propertyIds.Contains(x.PropertyId)).ExecuteDeleteAsync();
    await db.UtilityRates.Where(x => propertyIds.Contains(x.PropertyId)).ExecuteDeleteAsync();
    await db.Services.Where(x => x.PropertyId != null && propertyIds.Contains(x.PropertyId.Value)).ExecuteDeleteAsync();
    await db.Properties.Where(x => propertyIds.Contains(x.Id)).ExecuteDeleteAsync();
    await db.TenantPhoneChallenges.Where(x => demoUserIds.Contains(x.LandlordId)).ExecuteDeleteAsync();
    await db.TenantProfiles.Where(x => tenantProfileIds.Contains(x.Id)).ExecuteDeleteAsync();
    await db.Users.Where(x => demoUserIds.Contains(x.Id)).ExecuteDeleteAsync();
    await tx.CommitAsync();
    Console.WriteLine($"Reset complete. {blobNames.Count} uploaded blobs queued for local API cleanup.");
}
return 0;
