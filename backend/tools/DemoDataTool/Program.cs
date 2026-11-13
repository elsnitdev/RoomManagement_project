using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RoomManagement.Data;
using RoomManagement.Domain;
using RoomManagement.Domain.Constants;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;
using ServiceEntity = RoomManagement.Domain.Entities.Service;

// Run from the repository root. All accounts and properties belong to this fixture.
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
const string managerId = "rm-demo-manager";
const string managerNewId = "rm-demo-manager-new";
const string tenantAId = "rm-demo-tenant-a";
const string tenantBId = "rm-demo-tenant-b";
const string tenantCId = "rm-demo-tenant-c";
const string tenantDId = "rm-demo-tenant-d";
const string tenantEId = "rm-demo-tenant-e";
const string tenantFId = "rm-demo-tenant-f";
const string tenantGId = "rm-demo-tenant-g";
const string tenantHId = "rm-demo-tenant-h";

var demoUserIds = new[]
{
    adminId, landlordId, pendingLandlordId, managerId, managerNewId,
    tenantAId, tenantBId, tenantCId, tenantDId, tenantEId, tenantFId, tenantGId, tenantHId
};

var property1Id = Guid.Parse("d0000000-0000-4000-8000-000000000001"); // An Bình - Bình Thạnh (Manager 1 được giao)
var property2Id = Guid.Parse("d0000000-0000-4000-8000-000000000002"); // Thảo Điền - Thủ Đức (Chỉ Landlord)
var demoPropertyIds = new[] { property1Id, property2Id };

// Rooms: 0..4 in Property 1 (P101..P105), 5..6 in Property 2 (TD-201, TD-202)
var rooms = Enumerable.Range(1, 7).Select(i => Guid.Parse($"d0000000-0000-4000-8000-{i + 10:D12}")).ToArray();
// Contracts: 0=P101 (A), 1=P102 (B), 2=P103 (C+E+F co-tenancy), 3=P104 (D pending confirmation), 4=TD-201 (H in Property 2)
var contracts = Enumerable.Range(1, 5).Select(i => Guid.Parse($"d0000000-0000-4000-8000-{i + 20:D12}")).ToArray();
// Profiles for 8 tenants (A..H)
var profiles = Enumerable.Range(1, 8).Select(i => Guid.Parse($"d0000000-0000-4000-8000-{i + 30:D12}")).ToArray();

if (command == "status")
{
    var count = await db.Users.CountAsync(u => demoUserIds.Contains(u.Id));
    var propCount = await db.Properties.CountAsync(p => demoPropertyIds.Contains(p.Id));
    Console.WriteLine($"Demo accounts: {count}/{demoUserIds.Length}; demo properties: {propCount}/2");
    if (propCount > 0)
    {
        var demoRooms = db.Rooms.Where(r => demoPropertyIds.Contains(r.PropertyId)).Select(r => r.Id);
        var demoContracts = db.Contracts.Where(c => demoRooms.Contains(c.RoomId)).Select(c => c.Id);
        var demoInvoices = db.Invoices.Where(i => demoContracts.Contains(i.ContractId)).Select(i => i.Id);
        Console.WriteLine($"Property1Id (Manager assigned): {property1Id}");
        Console.WriteLine($"Property2Id (Landlord only):    {property2Id}");
        Console.WriteLine($"Rooms: {await demoRooms.CountAsync()}; contracts: {await demoContracts.CountAsync()}; invoices: {await demoInvoices.CountAsync()}; payments: {await db.Payments.CountAsync(p => demoInvoices.Contains(p.InvoiceId))}");
    }
    return 0;
}

if (command == "seed")
{
    if (await db.Users.AnyAsync(u => demoUserIds.Contains(u.Id) || u.UserName.StartsWith(prefix))
        || await db.Properties.AnyAsync(p => demoPropertyIds.Contains(p.Id)))
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
    var twoMonthsAgo = first.AddMonths(-2);
    var hasher = new PasswordHasher<AppUser>();

    AppUser MakeUser(
        string id, string name, string role, string fullName,
        string? phone = null, string? citizenId = null, DateOnly? dob = null, string? address = null,
        bool approved = true, string? ownerLandlordId = null, bool mustChangePassword = false,
        string verificationStatus = IdentityVerificationStatuses.Unverified)
    {
        var user = new AppUser
        {
            Id = id,
            UserName = prefix + name,
            NormalizedUserName = (prefix + name).ToUpperInvariant(),
            Email = $"{prefix}{name}@example.com",
            NormalizedEmail = $"{prefix}{name}@example.com".ToUpperInvariant(),
            FullName = fullName,
            PhoneNumber = phone,
            CitizenId = citizenId,
            DateOfBirth = dob,
            Address = address,
            Role = role,
            IsActive = approved,
            ApprovalStatus = approved ? ApprovalStatuses.Approved : ApprovalStatuses.Pending,
            ReviewedAt = approved ? now : null,
            LandlordUserId = ownerLandlordId,
            MustChangePassword = mustChangePassword,
            IdentityVerificationStatus = verificationStatus,
            IdentityVerifiedAt = verificationStatus == IdentityVerificationStatuses.Verified ? now : null,
            CreatedAt = now
        };
        user.PasswordHash = hasher.HashPassword(user, password);
        return user;
    }

    db.Users.AddRange(
        MakeUser(adminId, "admin", UserRoles.Admin, "Quản trị viên Hệ thống", "0900000001"),
        MakeUser(landlordId, "landlord", UserRoles.Landlord, "Trần Minh Hoàng", "0900000002", "079085000002", new DateOnly(1985, 5, 12), "123 Điện Biên Phủ, Bình Thạnh, TP.HCM"),
        MakeUser(pendingLandlordId, "landlord_pending", UserRoles.Landlord, "Phạm Văn Chờ", "0900000003", approved: false),
        MakeUser(managerId, "manager", UserRoles.Manager, "Nguyễn Quốc Bảo", "0900000010", "079092000010", new DateOnly(1992, 8, 20), "Bình Thạnh, TP.HCM", ownerLandlordId: landlordId, mustChangePassword: false),
        MakeUser(managerNewId, "manager_new", UserRoles.Manager, "Lê Thị Thu Hà", "0900000011", "079095000011", new DateOnly(1995, 3, 15), "Gò Vấp, TP.HCM", ownerLandlordId: landlordId, mustChangePassword: true),
        MakeUser(tenantAId, "tenant_a", UserRoles.Tenant, "Nguyễn Văn An", "0901234001", "079200001001", new DateOnly(2000, 1, 10), "Quận 1, TP.HCM", verificationStatus: IdentityVerificationStatuses.Unverified),
        MakeUser(tenantBId, "tenant_b", UserRoles.Tenant, "Trần Thị Bình", "0901234002", "079200001002", new DateOnly(1999, 4, 22), "Quận 3, TP.HCM", verificationStatus: IdentityVerificationStatuses.Verified),
        MakeUser(tenantCId, "tenant_c", UserRoles.Tenant, "Lê Minh Chi", "0901234003", "079200001003", new DateOnly(2001, 7, 18), "Quận 5, TP.HCM", verificationStatus: IdentityVerificationStatuses.Unverified),
        MakeUser(tenantDId, "tenant_d", UserRoles.Tenant, "Phạm Tuấn Dũng", "0901234004", "079200001004", new DateOnly(1998, 11, 5), "Quận 10, TP.HCM", verificationStatus: IdentityVerificationStatuses.Unverified),
        MakeUser(tenantEId, "tenant_e", UserRoles.Tenant, "Phạm Gia Huy", "0901234005", "079200001005", new DateOnly(2001, 9, 14), "Quận 7, TP.HCM", verificationStatus: IdentityVerificationStatuses.Unverified),
        MakeUser(tenantFId, "tenant_f", UserRoles.Tenant, "Võ Thảo Vy", "0901234006", "079200001006", new DateOnly(2002, 2, 28), "Tân Bình, TP.HCM", verificationStatus: IdentityVerificationStatuses.Unverified),
        MakeUser(tenantGId, "tenant_g", UserRoles.Tenant, "Hoàng Khánh Linh", "0901234007", "079200001007", new DateOnly(2000, 12, 1), "Phú Nhuận, TP.HCM", verificationStatus: IdentityVerificationStatuses.Unverified),
        MakeUser(tenantHId, "tenant_h", UserRoles.Tenant, "Đặng Ngọc Minh", "0901234008", "079200001008", new DateOnly(1997, 6, 30), "Thủ Đức, TP.HCM", verificationStatus: IdentityVerificationStatuses.Verified)
    );

    db.Properties.AddRange(
        new Property
        {
            Id = property1Id, OwnerUserId = landlordId,
            Name = "Nhà trọ An Bình - Bình Thạnh",
            Address = "123/45 Điện Biên Phủ, P.15, Q. Bình Thạnh, TP.HCM",
            BankName = "Vietcombank", BankAccountNumber = "1012345678", BankAccountHolder = "TRAN MINH HOANG",
            IsActive = true, CreatedAt = now
        },
        new Property
        {
            Id = property2Id, OwnerUserId = landlordId,
            Name = "Chung cư mini Thảo Điền - Thủ Đức",
            Address = "88 Quốc Hương, P. Thảo Điền, TP. Thủ Đức, TP.HCM",
            BankName = "Techcombank", BankAccountNumber = "19033445566", BankAccountHolder = "TRAN MINH HOANG",
            IsActive = true, CreatedAt = now
        }
    );

    // Assign both Managers to Property 1 ONLY (Property 2 remains Landlord-only)
    db.PropertyManagerAssignments.AddRange(
        new PropertyManagerAssignment { Id = Guid.NewGuid(), ManagerUserId = managerId, PropertyId = property1Id, AssignedByUserId = landlordId, AssignedAt = now },
        new PropertyManagerAssignment { Id = Guid.NewGuid(), ManagerUserId = managerNewId, PropertyId = property1Id, AssignedByUserId = landlordId, AssignedAt = now }
    );

    db.Rooms.AddRange(
        new Room { Id = rooms[0], PropertyId = property1Id, RoomNumber = "P101", BasePrice = 3_200_000, MaxOccupants = 2, Status = RoomStatus.Occupied, CreatedAt = now },
        new Room { Id = rooms[1], PropertyId = property1Id, RoomNumber = "P102", BasePrice = 3_000_000, MaxOccupants = 2, Status = RoomStatus.Occupied, CreatedAt = now },
        new Room { Id = rooms[2], PropertyId = property1Id, RoomNumber = "P103", BasePrice = 4_500_000, MaxOccupants = 4, Status = RoomStatus.Occupied, CreatedAt = now },
        new Room { Id = rooms[3], PropertyId = property1Id, RoomNumber = "P104", BasePrice = 2_800_000, MaxOccupants = 2, Status = RoomStatus.Available, CreatedAt = now },
        new Room { Id = rooms[4], PropertyId = property1Id, RoomNumber = "P105", BasePrice = 3_500_000, MaxOccupants = 3, Status = RoomStatus.Available, CreatedAt = now },
        new Room { Id = rooms[5], PropertyId = property2Id, RoomNumber = "TD-201", BasePrice = 5_500_000, MaxOccupants = 2, Status = RoomStatus.Occupied, CreatedAt = now },
        new Room { Id = rooms[6], PropertyId = property2Id, RoomNumber = "TD-202", BasePrice = 5_800_000, MaxOccupants = 2, Status = RoomStatus.Available, CreatedAt = now }
    );

    // Canonical self-registered style TenantProfiles (OwnerLandlordUserId = null)
    var tenantData = new[]
    {
        (Id: tenantAId, Name: "Nguyễn Văn An", Phone: "0901234001", Citizen: "079200001001", Dob: new DateOnly(2000, 1, 10), Gender: Gender.Male, Addr: "Quận 1, TP.HCM"),
        (Id: tenantBId, Name: "Trần Thị Bình", Phone: "0901234002", Citizen: "079200001002", Dob: new DateOnly(1999, 4, 22), Gender: Gender.Female, Addr: "Quận 3, TP.HCM"),
        (Id: tenantCId, Name: "Lê Minh Chi", Phone: "0901234003", Citizen: "079200001003", Dob: new DateOnly(2001, 7, 18), Gender: Gender.Female, Addr: "Quận 5, TP.HCM"),
        (Id: tenantDId, Name: "Phạm Tuấn Dũng", Phone: "0901234004", Citizen: "079200001004", Dob: new DateOnly(1998, 11, 5), Gender: Gender.Male, Addr: "Quận 10, TP.HCM"),
        (Id: tenantEId, Name: "Phạm Gia Huy", Phone: "0901234005", Citizen: "079200001005", Dob: new DateOnly(2001, 9, 14), Gender: Gender.Male, Addr: "Quận 7, TP.HCM"),
        (Id: tenantFId, Name: "Võ Thảo Vy", Phone: "0901234006", Citizen: "079200001006", Dob: new DateOnly(2002, 2, 28), Gender: Gender.Female, Addr: "Tân Bình, TP.HCM"),
        (Id: tenantGId, Name: "Hoàng Khánh Linh", Phone: "0901234007", Citizen: "079200001007", Dob: new DateOnly(2000, 12, 1), Gender: Gender.Female, Addr: "Phú Nhuận, TP.HCM"),
        (Id: tenantHId, Name: "Đặng Ngọc Minh", Phone: "0901234008", Citizen: "079200001008", Dob: new DateOnly(1997, 6, 30), Gender: Gender.Male, Addr: "Thủ Đức, TP.HCM")
    };
    for (var i = 0; i < profiles.Length; i++)
    {
        var t = tenantData[i];
        db.TenantProfiles.Add(new TenantProfile
        {
            Id = profiles[i],
            UserId = t.Id,
            OwnerLandlordUserId = null,
            CreatedByUserId = t.Id,
            FullName = t.Name,
            PhoneNumber = t.Phone,
            Email = $"{prefix}tenant_{(char)('a' + i)}@example.com",
            CitizenId = t.Citizen,
            DateOfBirth = t.Dob,
            Gender = t.Gender,
            PermanentAddress = t.Addr,
            CreatedAt = now
        });
    }

    // Active Property Rules for Property 1 & Property 2
    var rules1Id = Guid.NewGuid();
    var rules2Id = Guid.NewGuid();
    db.ContractRulesVersions.AddRange(
        new ContractRulesVersion
        {
            Id = rules1Id, PropertyId = property1Id, IsActive = true, VersionNumber = 1,
            Content = "1. Giữ gìn vệ sinh chung.\n2. Không gây ồn ào sau 22h00.\n3. Thanh toán tiền phòng đúng hạn trước ngày 05 hàng tháng.\n4. Không tự ý khoan đục tường hoặc nuôi thú cưng khi chưa có sự đồng ý.",
            CreatedByUserId = landlordId, CreatedAt = now
        },
        new ContractRulesVersion
        {
            Id = rules2Id, PropertyId = property2Id, IsActive = true, VersionNumber = 1,
            Content = "1. Tuân thủ quy định PCCC và ra vào bằng thẻ từ.\n2. Thanh toán đúng hạn trước ngày 05 hàng tháng.",
            CreatedByUserId = landlordId, CreatedAt = now
        }
    );

    // Contract 3 (P104) is PendingTenantConfirmation for tenant D
    var p104Snapshot = JsonSerializer.Serialize(new
    {
        ContractId = contracts[3],
        ContractCode = "HD-AB-P104",
        PropertyId = property1Id,
        RoomId = rooms[3],
        RoomNumber = "P104",
        StartDate = first,
        EndDate = first.AddMonths(12),
        MonthlyRent = 2_800_000m,
        DepositAmount = 2_800_000m,
        PaymentDueDay = (byte)5,
        TenancyMode = "SingleOrFamily",
        RulesVersionId = rules1Id
    });
    var p104Hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(p104Snapshot))).ToLowerInvariant();

    db.Contracts.AddRange(
        // 0: P101 - Tenant A (Active, single)
        new Contract
        {
            Id = contracts[0], ContractCode = "HD-AB-P101", RoomId = rooms[0],
            StartDate = twoMonthsAgo, EndDate = twoMonthsAgo.AddMonths(12),
            MonthlyRent = 3_200_000, DepositAmount = 3_200_000, PaymentDueDay = 5,
            Status = ContractStatus.Active, CreatedAt = now,
            OperatorConfirmedByUserId = managerId, OperatorConfirmedAt = now
        },
        // 1: P102 - Tenant B (Active, single, has paid + pending payment)
        new Contract
        {
            Id = contracts[1], ContractCode = "HD-AB-P102", RoomId = rooms[1],
            StartDate = twoMonthsAgo, EndDate = twoMonthsAgo.AddMonths(12),
            MonthlyRent = 3_000_000, DepositAmount = 3_000_000, PaymentDueDay = 5,
            Status = ContractStatus.Active, CreatedAt = now,
            OperatorConfirmedByUserId = landlordId, OperatorConfirmedAt = now
        },
        // 2: P103 - CoTenancy 3 người (Tenant C đại diện, E, F) - Active, chưa có hóa đơn kỳ này
        new Contract
        {
            Id = contracts[2], ContractCode = "HD-AB-P103", RoomId = rooms[2],
            StartDate = previousMonth, EndDate = previousMonth.AddMonths(12),
            MonthlyRent = 4_500_000, DepositAmount = 4_500_000, PaymentDueDay = 5,
            TenancyMode = ContractTenancyMode.CoTenancy,
            Status = ContractStatus.Active, CreatedAt = now,
            OperatorConfirmedByUserId = managerId, OperatorConfirmedAt = now
        },
        // 3: P104 - PendingTenantConfirmation (Tenant D xem & xác nhận hợp đồng + nội quy)
        new Contract
        {
            Id = contracts[3], ContractCode = "HD-AB-P104", RoomId = rooms[3],
            StartDate = first, EndDate = first.AddMonths(12),
            MonthlyRent = 2_800_000, DepositAmount = 2_800_000, PaymentDueDay = 5,
            Status = ContractStatus.PendingTenantConfirmation,
            OfferVersion = 1, OfferTermsHash = p104Hash, OfferSnapshotJson = p104Snapshot,
            OfferRulesVersionId = rules1Id, OfferSentAt = now, OfferSentByUserId = managerId,
            Note = "Hợp đồng thuê 12 tháng, đã bao gồm nội quy khu trọ.",
            CreatedAt = now
        },
        // 4: TD-201 - Property 2 (Tenant H) - Chỉ Landlord thấy
        new Contract
        {
            Id = contracts[4], ContractCode = "HD-TD-201", RoomId = rooms[5],
            StartDate = previousMonth, EndDate = previousMonth.AddMonths(12),
            MonthlyRent = 5_500_000, DepositAmount = 5_500_000, PaymentDueDay = 5,
            Status = ContractStatus.Active, CreatedAt = now,
            OperatorConfirmedByUserId = landlordId, OperatorConfirmedAt = now
        }
    );

    db.ContractTenants.AddRange(
        new ContractTenant { Id = Guid.NewGuid(), ContractId = contracts[0], TenantId = profiles[0], JoinDate = twoMonthsAgo, IsContactPerson = true, Role = ContractTenantRole.ContractParty, CreatedAt = now },
        new ContractTenant { Id = Guid.NewGuid(), ContractId = contracts[1], TenantId = profiles[1], JoinDate = twoMonthsAgo, IsContactPerson = true, Role = ContractTenantRole.ContractParty, CreatedAt = now },
        // P103: 3 người trong 1 hợp đồng
        new ContractTenant { Id = Guid.NewGuid(), ContractId = contracts[2], TenantId = profiles[2], JoinDate = previousMonth, IsContactPerson = true, Role = ContractTenantRole.ContractParty, CreatedAt = now },
        new ContractTenant { Id = Guid.NewGuid(), ContractId = contracts[2], TenantId = profiles[4], JoinDate = previousMonth, IsContactPerson = false, Role = ContractTenantRole.ContractParty, CreatedAt = now },
        new ContractTenant { Id = Guid.NewGuid(), ContractId = contracts[2], TenantId = profiles[5], JoinDate = previousMonth, IsContactPerson = false, Role = ContractTenantRole.ContractParty, CreatedAt = now },
        // P104: Pending confirmation
        new ContractTenant { Id = Guid.NewGuid(), ContractId = contracts[3], TenantId = profiles[3], JoinDate = first, IsContactPerson = true, Role = ContractTenantRole.ContractParty, CreatedAt = now },
        // TD-201: Property 2
        new ContractTenant { Id = Guid.NewGuid(), ContractId = contracts[4], TenantId = profiles[7], JoinDate = previousMonth, IsContactPerson = true, Role = ContractTenantRole.ContractParty, CreatedAt = now }
    );

    db.ContractOfferEvents.Add(new ContractOfferEvent
    {
        Id = Guid.NewGuid(), ContractId = contracts[3], OfferVersion = 1, Action = "Sent",
        TermsHash = p104Hash, RulesVersionId = rules1Id, SnapshotJson = p104Snapshot,
        ActorUserId = managerId, CreatedAt = now
    });

    db.UserNotifications.Add(new UserNotification
    {
        Id = Guid.NewGuid(), UserId = tenantDId, Type = "ContractOfferSent",
        Title = "Đề nghị hợp đồng mới",
        Message = "Bạn nhận được đề nghị hợp đồng HD-AB-P104 (phiên bản 1) cho phòng P104. Vui lòng xem và xác nhận.",
        ContractId = contracts[3], PropertyId = property1Id, ActorUserId = managerId, IsRead = false, CreatedAt = now
    });

    // Utility rates for both properties
    foreach (var pid in demoPropertyIds)
    {
        foreach (var type in new[] { UtilityType.Electricity, UtilityType.Water })
        {
            db.UtilityRates.Add(new UtilityRate
            {
                Id = Guid.NewGuid(), PropertyId = pid, UtilityType = type,
                UnitPrice = type == UtilityType.Electricity ? 3_500 : 15_000,
                Unit = type == UtilityType.Electricity ? "kWh" : "m³",
                EffectiveFrom = twoMonthsAgo, CreatedAt = now
            });
        }
    }

    // Meter readings for P101, P102 (twoMonthsAgo + previousMonth), P103 (only initial baseline for twoMonthsAgo so Manager can enter previousMonth/currentMonth)
    db.MeterReadings.AddRange(
        new MeterReading { Id = Guid.NewGuid(), RoomId = rooms[0], ContractId = contracts[0], MeterType = UtilityType.Electricity, Year = (short)previousMonth.Year, Month = (byte)previousMonth.Month, PreviousValue = 100, CurrentValue = 180, RecordedAt = now },
        new MeterReading { Id = Guid.NewGuid(), RoomId = rooms[0], ContractId = contracts[0], MeterType = UtilityType.Water, Year = (short)previousMonth.Year, Month = (byte)previousMonth.Month, PreviousValue = 20, CurrentValue = 32, RecordedAt = now },
        new MeterReading { Id = Guid.NewGuid(), RoomId = rooms[1], ContractId = contracts[1], MeterType = UtilityType.Electricity, Year = (short)twoMonthsAgo.Year, Month = (byte)twoMonthsAgo.Month, PreviousValue = 50, CurrentValue = 100, RecordedAt = now },
        new MeterReading { Id = Guid.NewGuid(), RoomId = rooms[1], ContractId = contracts[1], MeterType = UtilityType.Water, Year = (short)twoMonthsAgo.Year, Month = (byte)twoMonthsAgo.Month, PreviousValue = 10, CurrentValue = 18, RecordedAt = now },
        new MeterReading { Id = Guid.NewGuid(), RoomId = rooms[1], ContractId = contracts[1], MeterType = UtilityType.Electricity, Year = (short)previousMonth.Year, Month = (byte)previousMonth.Month, PreviousValue = 100, CurrentValue = 160, RecordedAt = now },
        new MeterReading { Id = Guid.NewGuid(), RoomId = rooms[1], ContractId = contracts[1], MeterType = UtilityType.Water, Year = (short)previousMonth.Year, Month = (byte)previousMonth.Month, PreviousValue = 18, CurrentValue = 26, RecordedAt = now }
    );

    var internetId = Guid.NewGuid();
    var parkingId = Guid.NewGuid();
    var trashId = Guid.NewGuid();
    db.Services.AddRange(
        new ServiceEntity { Id = internetId, PropertyId = property1Id, Name = "Internet cáp quang", Unit = "phòng/tháng", DefaultPrice = 100_000, CreatedAt = now },
        new ServiceEntity { Id = parkingId, PropertyId = property1Id, Name = "Giữ xe máy", Unit = "xe/tháng", DefaultPrice = 80_000, CreatedAt = now },
        new ServiceEntity { Id = trashId, PropertyId = property1Id, Name = "Vệ sinh & Rác", Unit = "phòng/tháng", DefaultPrice = 30_000, CreatedAt = now }
    );
    db.RoomServices.AddRange(
        new RoomService { Id = Guid.NewGuid(), RoomId = rooms[0], ServiceId = internetId, StartDate = twoMonthsAgo, CreatedAt = now },
        new RoomService { Id = Guid.NewGuid(), RoomId = rooms[1], ServiceId = parkingId, StartDate = twoMonthsAgo, CreatedAt = now },
        new RoomService { Id = Guid.NewGuid(), RoomId = rooms[2], ServiceId = internetId, StartDate = previousMonth, CreatedAt = now },
        new RoomService { Id = Guid.NewGuid(), RoomId = rooms[2], ServiceId = trashId, StartDate = previousMonth, CreatedAt = now }
    );

    // Invoices:
    // - P101 (previousMonth): Issued, unpaid -> Tenant A can pay, Manager can record/approve
    // - P102 (twoMonthsAgo): Issued, paid (Success)
    // - P102 (previousMonth): Issued, has Pending BankTransfer payment -> Manager can approve/reject
    var invoiceP101 = Guid.NewGuid();
    var invoiceP102Old = Guid.NewGuid();
    var invoiceP102New = Guid.NewGuid();
    var issuedPrev = previousMonth.AddMonths(1);
    var issuedTwo = twoMonthsAgo.AddMonths(1);

    db.Invoices.AddRange(
        new Invoice { Id = invoiceP101, ContractId = contracts[0], RoomId = rooms[0], InvoiceCode = $"INV-P101-{previousMonth:yyyyMM}", InvoiceType = InvoiceType.Monthly, BillingYear = (short)previousMonth.Year, BillingMonth = (byte)previousMonth.Month, IssueDate = issuedPrev, DueDate = issuedPrev.AddDays(4), TotalAmount = 3_760_000, Status = InvoiceStatus.Issued, CreatedAt = now },
        new Invoice { Id = invoiceP102Old, ContractId = contracts[1], RoomId = rooms[1], InvoiceCode = $"INV-P102-{twoMonthsAgo:yyyyMM}", InvoiceType = InvoiceType.Monthly, BillingYear = (short)twoMonthsAgo.Year, BillingMonth = (byte)twoMonthsAgo.Month, IssueDate = issuedTwo, DueDate = issuedTwo.AddDays(4), TotalAmount = 3_375_000, Status = InvoiceStatus.Issued, CreatedAt = now },
        new Invoice { Id = invoiceP102New, ContractId = contracts[1], RoomId = rooms[1], InvoiceCode = $"INV-P102-{previousMonth:yyyyMM}", InvoiceType = InvoiceType.Monthly, BillingYear = (short)previousMonth.Year, BillingMonth = (byte)previousMonth.Month, IssueDate = issuedPrev, DueDate = issuedPrev.AddDays(4), TotalAmount = 3_410_000, Status = InvoiceStatus.Issued, CreatedAt = now }
    );

    void Item(Guid invoiceId, InvoiceItemType type, string description, decimal quantity, decimal price) =>
        db.InvoiceItems.Add(new InvoiceItem { Id = Guid.NewGuid(), InvoiceId = invoiceId, ItemType = type, Description = description, Quantity = quantity, UnitPrice = price, Amount = quantity * price, CreatedAt = now });

    Item(invoiceP101, InvoiceItemType.Rent, "Tiền phòng P101", 1, 3_200_000);
    Item(invoiceP101, InvoiceItemType.Electricity, "Điện (100 -> 180)", 80, 3_500);
    Item(invoiceP101, InvoiceItemType.Water, "Nước (20 -> 32)", 12, 15_000);
    Item(invoiceP101, InvoiceItemType.Service, "Internet cáp quang", 1, 100_000);

    Item(invoiceP102Old, InvoiceItemType.Rent, "Tiền phòng P102", 1, 3_000_000);
    Item(invoiceP102Old, InvoiceItemType.Electricity, "Điện (50 -> 100)", 50, 3_500);
    Item(invoiceP102Old, InvoiceItemType.Water, "Nước (10 -> 18)", 8, 15_000);
    Item(invoiceP102Old, InvoiceItemType.Service, "Giữ xe máy", 1, 80_000);

    Item(invoiceP102New, InvoiceItemType.Rent, "Tiền phòng P102", 1, 3_000_000);
    Item(invoiceP102New, InvoiceItemType.Electricity, "Điện (100 -> 160)", 60, 3_500);
    Item(invoiceP102New, InvoiceItemType.Water, "Nước (18 -> 26)", 8, 15_000);
    Item(invoiceP102New, InvoiceItemType.Service, "Giữ xe máy", 1, 80_000);

    db.Payments.AddRange(
        new Payment
        {
            Id = Guid.NewGuid(), InvoiceId = invoiceP102Old, SubmittedByUserId = tenantBId,
            ClaimedAmount = 3_375_000, VerifiedAmount = 3_375_000, PaymentMethod = PaymentMethod.BankTransfer,
            Status = PaymentStatus.Success, PaidAt = now.AddDays(-25), VerifiedAt = now.AddDays(-24), VerifiedByUserId = landlordId,
            IdempotencyKey = $"rm-demo-p102-paid-{twoMonthsAgo:yyyyMM}", CreatedAt = now.AddDays(-25)
        },
        new Payment
        {
            Id = Guid.NewGuid(), InvoiceId = invoiceP102New, SubmittedByUserId = tenantBId,
            ClaimedAmount = 3_410_000, PaymentMethod = PaymentMethod.BankTransfer,
            Status = PaymentStatus.Pending, PaidAt = now,
            IdempotencyKey = $"rm-demo-p102-pending-{previousMonth:yyyyMM}", CreatedAt = now
        }
    );

    await db.SaveChangesAsync();
    await tx.CommitAsync();
    Console.WriteLine("Seeded realistic demo data successfully!");
    Console.WriteLine($"Local password file: {passwordPath}");
    Console.WriteLine($"Property 1 (An Bình - Bình Thạnh, assigned to Manager): {property1Id}");
    Console.WriteLine($"Property 2 (Thảo Điền - Thủ Đức, Landlord only):     {property2Id}");
    return 0;
}

// Reset is restricted to the demo properties, demo profiles, and fixed demo accounts.
await using (var tx = await db.Database.BeginTransactionAsync())
{
    var knownDemoUsers = await db.Users.Where(u => demoUserIds.Contains(u.Id)).ToListAsync();
    if (knownDemoUsers.Any(u => !u.UserName.StartsWith(prefix, StringComparison.Ordinal)))
        throw new InvalidOperationException("A reserved demo user ID belongs to a non-demo account. Reset aborted.");

    var propertyIds = db.Properties.Where(p => demoPropertyIds.Contains(p.Id) || p.OwnerUserId == landlordId || p.OwnerUserId == pendingLandlordId).Select(p => p.Id);
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

    await db.UserNotifications.Where(x => demoUserIds.Contains(x.UserId) || (x.ContractId != null && contractIds.Contains(x.ContractId.Value))).ExecuteDeleteAsync();
    await db.TenantLookupAudits.Where(x => demoUserIds.Contains(x.ActorUserId) || demoUserIds.Contains(x.OwnerLandlordUserId)).ExecuteDeleteAsync();
    await db.ContractOfferEvents.Where(x => contractIds.Contains(x.ContractId)).ExecuteDeleteAsync();
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
    await db.PropertyManagerAssignments.Where(x => propertyIds.Contains(x.PropertyId) || demoUserIds.Contains(x.ManagerUserId)).ExecuteDeleteAsync();
    await db.Properties.Where(x => propertyIds.Contains(x.Id)).ExecuteDeleteAsync();
    await db.TenantPhoneChallenges.Where(x => demoUserIds.Contains(x.LandlordId)).ExecuteDeleteAsync();
    await db.TenantProfiles.Where(x => tenantProfileIds.Contains(x.Id)).ExecuteDeleteAsync();
    await db.Users.Where(x => demoUserIds.Contains(x.Id)).ExecuteDeleteAsync();
    await tx.CommitAsync();
    Console.WriteLine($"Reset complete. {blobNames.Count} uploaded blobs queued for local API cleanup.");
}
return 0;
