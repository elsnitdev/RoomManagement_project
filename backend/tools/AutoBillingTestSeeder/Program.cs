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

// Run from the repository root. All accounts, properties, contracts, and billing inputs belong to this isolated fixture.
var command = args.SingleOrDefault()?.ToLowerInvariant();
if (command is not ("seed" or "reset" or "status"))
{
    Console.Error.WriteLine("Usage: dotnet run --project backend/tools/AutoBillingTestSeeder -- seed|status|reset");
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
{
    throw new InvalidOperationException("AutoBillingTestSeeder only accepts a RoomManagement database.");
}

if ((await db.Database.GetPendingMigrationsAsync()).Any())
    throw new InvalidOperationException("Apply pending migrations before using AutoBillingTestSeeder.");

Console.WriteLine($"Database: {databaseName}");

const string prefix = "rm_autobill_";
const string landlordId = "rm-autobill-landlord";
const string managerId = "rm-autobill-manager";
const string tenant1Id = "rm-autobill-tenant-1";
const string tenant2Id = "rm-autobill-tenant-2";
const string tenant3Id = "rm-autobill-tenant-3";
const string tenant4Id = "rm-autobill-tenant-4";

var fixtureUserIds = new[]
{
    landlordId, managerId, tenant1Id, tenant2Id, tenant3Id, tenant4Id
};

var propertyId = Guid.Parse("a0000000-0000-4000-8000-000000000001");
var roomIds = Enumerable.Range(1, 5)
    .Select(i => Guid.Parse($"a0000000-0000-4000-8000-{i + 10:D12}"))
    .ToArray();
var contractIds = Enumerable.Range(1, 5)
    .Select(i => Guid.Parse($"a0000000-0000-4000-8000-{i + 20:D12}"))
    .ToArray();
var profileIds = Enumerable.Range(1, 4)
    .Select(i => Guid.Parse($"a0000000-0000-4000-8000-{i + 30:D12}"))
    .ToArray();

var localToday = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
var currentMonthStart = new DateOnly(localToday.Year, localToday.Month, 1);
var targetBillingMonth = currentMonthStart.AddMonths(-1); // MonthlyWorklist default period
var baselineReadingMonth = currentMonthStart.AddMonths(-2); // Prior month baseline readings

if (command == "status")
{
    var userCount = await db.Users.CountAsync(u => fixtureUserIds.Contains(u.Id));
    var propCount = await db.Properties.CountAsync(p => p.Id == propertyId);
    Console.WriteLine($"Auto-billing accounts: {userCount}/{fixtureUserIds.Length}; property: {propCount}/1");
    if (propCount > 0)
    {
        var contractsCount = await db.Contracts.CountAsync(c => contractIds.Contains(c.Id));
        var activeContractsCount = await db.Contracts.CountAsync(c => contractIds.Contains(c.Id) && c.Status == ContractStatus.Active);
        var rateCount = await db.UtilityRates.CountAsync(r => r.PropertyId == propertyId);
        var roomServiceCount = await db.RoomServices.CountAsync(rs => roomIds.Contains(rs.RoomId));
        var baselineReadingCount = await db.MeterReadings.CountAsync(m =>
            roomIds.Contains(m.RoomId) && m.Year == baselineReadingMonth.Year && m.Month == baselineReadingMonth.Month);
        var targetReadingCount = await db.MeterReadings.CountAsync(m =>
            roomIds.Contains(m.RoomId) && m.Year == targetBillingMonth.Year && m.Month == targetBillingMonth.Month);
        var targetInvoiceCount = await db.Invoices.CountAsync(i =>
            contractIds.Contains(i.ContractId) &&
            i.InvoiceType == InvoiceType.Monthly &&
            i.BillingYear == targetBillingMonth.Year &&
            i.BillingMonth == targetBillingMonth.Month &&
            i.Status != InvoiceStatus.Cancelled);

        Console.WriteLine($"PropertyId: {propertyId}");
        Console.WriteLine($"Rooms: {await db.Rooms.CountAsync(r => roomIds.Contains(r.Id))}/5; Contracts: {contractsCount}/5 (Active: {activeContractsCount}/5)");
        Console.WriteLine($"UtilityRates: {rateCount}/2; RoomServices: {roomServiceCount}; BaselineReadings ({baselineReadingMonth:yyyy-MM}): {baselineReadingCount}/10");
        Console.WriteLine($"Target Billing Period ({targetBillingMonth:yyyy-MM}): MeterReadings={targetReadingCount}/10, MonthlyInvoices={targetInvoiceCount}/5");
    }
    return 0;
}

if (command == "seed")
{
    if (await db.Users.AnyAsync(u => fixtureUserIds.Contains(u.Id) || u.UserName.StartsWith(prefix))
        || await db.Properties.AnyAsync(p => p.Id == propertyId))
    {
        throw new InvalidOperationException("Auto-billing test data already exists. Run status or reset first.");
    }

    var passwordPath = Path.GetFullPath("backend/tools/AutoBillingTestSeeder/.autobill-password");
    var password = Environment.GetEnvironmentVariable("ROOMMANAGEMENT_AUTOBILL_PASSWORD")
        ?? (File.Exists(passwordPath)
            ? (await File.ReadAllTextAsync(passwordPath)).Trim()
            : Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)) + "!a1");
    if (string.IsNullOrWhiteSpace(password) || password.Length < 12)
        throw new InvalidOperationException("Auto-billing password must contain at least 12 characters.");
    await File.WriteAllTextAsync(passwordPath, password);

    await using var tx = await db.Database.BeginTransactionAsync();
    var now = DateTime.UtcNow;
    var hasher = new PasswordHasher<AppUser>();

    AppUser MakeUser(
        string id, string name, string role, string fullName,
        string phone, string citizenId, DateOnly dob, string address,
        string? ownerLandlordId = null,
        string verificationStatus = IdentityVerificationStatuses.Verified,
        string? customEmail = null)
    {
        var email = string.IsNullOrWhiteSpace(customEmail) ? $"{prefix}{name}@example.com" : customEmail.Trim();
        var user = new AppUser
        {
            Id = id,
            UserName = prefix + name,
            NormalizedUserName = (prefix + name).ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            FullName = fullName,
            PhoneNumber = phone,
            CitizenId = citizenId,
            DateOfBirth = dob,
            Address = address,
            Role = role,
            IsActive = true,
            ApprovalStatus = ApprovalStatuses.Approved,
            ReviewedAt = now,
            LandlordUserId = ownerLandlordId,
            MustChangePassword = false,
            IdentityVerificationStatus = verificationStatus,
            IdentityVerifiedAt = verificationStatus == IdentityVerificationStatuses.Verified ? now : null,
            CreatedAt = now
        };
        user.PasswordHash = hasher.HashPassword(user, password);
        return user;
    }

    const string targetPhone = "0869929022";
    const string targetEmail1 = "tinsle0609@gmail.com";
    const string targetEmail2 = "aihieuchotoi987@gmail.com";

    var existingEmail1User = await db.Users.FirstOrDefaultAsync(u => u.DeletedAt == null && u.NormalizedEmail == targetEmail1.ToUpperInvariant());
    var existingEmail2User = await db.Users.FirstOrDefaultAsync(u => u.DeletedAt == null && u.NormalizedEmail == targetEmail2.ToUpperInvariant());
    var existingPhoneUser = await db.Users.FirstOrDefaultAsync(u => u.DeletedAt == null && u.PhoneNumber == targetPhone);

    db.Users.AddRange(
        MakeUser(landlordId, "landlord", UserRoles.Landlord, "Lê Hoàng Nam", "0910000001", "079082000101", new DateOnly(1982, 4, 15), "45 Phan Xích Long, Phú Nhuận, TP.HCM"),
        MakeUser(managerId, "manager", UserRoles.Manager, "Ngô Quang Khải", "0910000002", "079090000102", new DateOnly(1990, 9, 20), "Phú Nhuận, TP.HCM", ownerLandlordId: landlordId)
    );

    // Tenant 1: ưu tiên dùng tài khoản có email tinsle0609@gmail.com hoặc tạo mới với SĐT 0869929022 (nếu chưa trùng)
    string resolvedT1UserId;
    Guid resolvedT1ProfileId;
    if (existingEmail1User is not null)
    {
        resolvedT1UserId = existingEmail1User.Id;
        var existingP1 = await db.TenantProfiles.FirstOrDefaultAsync(p => p.UserId == existingEmail1User.Id);
        if (existingP1 is not null)
        {
            resolvedT1ProfileId = existingP1.Id;
        }
        else
        {
            resolvedT1ProfileId = profileIds[0];
            db.TenantProfiles.Add(new TenantProfile
            {
                Id = resolvedT1ProfileId, UserId = resolvedT1UserId, OwnerLandlordUserId = null, CreatedByUserId = resolvedT1UserId,
                FullName = existingEmail1User.FullName ?? "Phạm Minh Tuấn",
                PhoneNumber = existingEmail1User.PhoneNumber ?? (existingPhoneUser is null ? targetPhone : "0911110001"),
                Email = existingEmail1User.Email, CitizenId = existingEmail1User.CitizenId,
                DateOfBirth = existingEmail1User.DateOfBirth, PermanentAddress = existingEmail1User.Address, CreatedAt = now
            });
        }
    }
    else
    {
        var t1Phone = existingPhoneUser is null ? targetPhone : "0911110001";
        var u1 = MakeUser(tenant1Id, "tenant_1", UserRoles.Tenant, "Phạm Minh Tuấn", t1Phone, "079200002001", new DateOnly(1998, 2, 14), "Quận 3, TP.HCM", customEmail: targetEmail1);
        db.Users.Add(u1);
        resolvedT1UserId = tenant1Id;
        resolvedT1ProfileId = profileIds[0];
        db.TenantProfiles.Add(new TenantProfile
        {
            Id = resolvedT1ProfileId, UserId = tenant1Id, OwnerLandlordUserId = null, CreatedByUserId = tenant1Id,
            FullName = "Phạm Minh Tuấn", PhoneNumber = t1Phone, Email = targetEmail1,
            CitizenId = "079200002001", DateOfBirth = new DateOnly(1998, 2, 14), Gender = Gender.Male, PermanentAddress = "Quận 3, TP.HCM", CreatedAt = now
        });
    }

    // Tenant 2: ưu tiên dùng tài khoản có email aihieuchotoi987@gmail.com hoặc tạo mới
    string resolvedT2UserId;
    Guid resolvedT2ProfileId;
    if (existingEmail2User is not null)
    {
        resolvedT2UserId = existingEmail2User.Id;
        var existingP2 = await db.TenantProfiles.FirstOrDefaultAsync(p => p.UserId == existingEmail2User.Id);
        if (existingP2 is not null)
        {
            resolvedT2ProfileId = existingP2.Id;
        }
        else
        {
            resolvedT2ProfileId = profileIds[1];
            db.TenantProfiles.Add(new TenantProfile
            {
                Id = resolvedT2ProfileId, UserId = resolvedT2UserId, OwnerLandlordUserId = null, CreatedByUserId = resolvedT2UserId,
                FullName = existingEmail2User.FullName ?? "Nguyễn Thảo Nhi",
                PhoneNumber = existingEmail2User.PhoneNumber ?? "0911110002",
                Email = existingEmail2User.Email, CitizenId = existingEmail2User.CitizenId,
                DateOfBirth = existingEmail2User.DateOfBirth, PermanentAddress = existingEmail2User.Address, CreatedAt = now
            });
        }
    }
    else
    {
        var u2 = MakeUser(tenant2Id, "tenant_2", UserRoles.Tenant, "Nguyễn Thảo Nhi", "0911110002", "079200002002", new DateOnly(1999, 6, 8), "Bình Thạnh, TP.HCM", customEmail: targetEmail2);
        db.Users.Add(u2);
        resolvedT2UserId = tenant2Id;
        resolvedT2ProfileId = profileIds[1];
        db.TenantProfiles.Add(new TenantProfile
        {
            Id = resolvedT2ProfileId, UserId = tenant2Id, OwnerLandlordUserId = null, CreatedByUserId = tenant2Id,
            FullName = "Nguyễn Thảo Nhi", PhoneNumber = "0911110002", Email = targetEmail2,
            CitizenId = "079200002002", DateOfBirth = new DateOnly(1999, 6, 8), Gender = Gender.Female, PermanentAddress = "Bình Thạnh, TP.HCM", CreatedAt = now
        });
    }

    // Nếu số điện thoại 0869929022 thuộc về một tài khoản riêng đã có sẵn (không phải tenant1/tenant2), dùng làm tenant3; ngược lại tạo tenant3 mới
    string resolvedT3UserId;
    Guid resolvedT3ProfileId;
    if (existingPhoneUser is not null && existingPhoneUser.Id != resolvedT1UserId && existingPhoneUser.Id != resolvedT2UserId)
    {
        resolvedT3UserId = existingPhoneUser.Id;
        var existingP3 = await db.TenantProfiles.FirstOrDefaultAsync(p => p.UserId == existingPhoneUser.Id);
        if (existingP3 is not null)
        {
            resolvedT3ProfileId = existingP3.Id;
        }
        else
        {
            resolvedT3ProfileId = profileIds[2];
            db.TenantProfiles.Add(new TenantProfile
            {
                Id = resolvedT3ProfileId, UserId = resolvedT3UserId, OwnerLandlordUserId = null, CreatedByUserId = resolvedT3UserId,
                FullName = existingPhoneUser.FullName ?? "Trần Quốc Việt",
                PhoneNumber = existingPhoneUser.PhoneNumber, Email = existingPhoneUser.Email, CitizenId = existingPhoneUser.CitizenId,
                DateOfBirth = existingPhoneUser.DateOfBirth, PermanentAddress = existingPhoneUser.Address, CreatedAt = now
            });
        }
    }
    else
    {
        db.Users.Add(MakeUser(tenant3Id, "tenant_3", UserRoles.Tenant, "Trần Quốc Việt", "0911110003", "079200002003", new DateOnly(1997, 11, 25), "Tân Bình, TP.HCM"));
        resolvedT3UserId = tenant3Id;
        resolvedT3ProfileId = profileIds[2];
        db.TenantProfiles.Add(new TenantProfile
        {
            Id = resolvedT3ProfileId, UserId = tenant3Id, OwnerLandlordUserId = null, CreatedByUserId = tenant3Id,
            FullName = "Trần Quốc Việt", PhoneNumber = "0911110003", Email = $"{prefix}tenant_3@example.com",
            CitizenId = "079200002003", DateOfBirth = new DateOnly(1997, 11, 25), Gender = Gender.Male, PermanentAddress = "Tân Bình, TP.HCM", CreatedAt = now
        });
    }

    db.Users.Add(MakeUser(tenant4Id, "tenant_4", UserRoles.Tenant, "Lê Ngọc Ánh", "0911110004", "079200002004", new DateOnly(2001, 3, 19), "Gò Vấp, TP.HCM", verificationStatus: IdentityVerificationStatuses.Unverified));
    db.TenantProfiles.Add(new TenantProfile
    {
        Id = profileIds[3], UserId = tenant4Id, OwnerLandlordUserId = null, CreatedByUserId = tenant4Id,
        FullName = "Lê Ngọc Ánh", PhoneNumber = "0911110004", Email = $"{prefix}tenant_4@example.com",
        CitizenId = "079200002004", DateOfBirth = new DateOnly(2001, 3, 19), Gender = Gender.Female, PermanentAddress = "Gò Vấp, TP.HCM", CreatedAt = now
    });

    db.Properties.Add(new Property
    {
        Id = propertyId,
        OwnerUserId = landlordId,
        Name = "Khu trọ Test Auto-Billing - Phú Nhuận",
        City = "Thành phố Hồ Chí Minh",
        Ward = "Phường 2",
        StreetAddress = "45/12 Phan Xích Long",
        Address = "45/12 Phan Xích Long, Phường 2, Thành phố Hồ Chí Minh",
        BankName = "Vietcombank",
        BankAccountNumber = "1029384756",
        BankAccountHolder = "LE HOANG NAM",
        IsActive = true,
        CreatedAt = now
    });

    db.PropertyManagerAssignments.Add(new PropertyManagerAssignment
    {
        Id = Guid.NewGuid(),
        ManagerUserId = managerId,
        PropertyId = propertyId,
        AssignedByUserId = landlordId,
        AssignedAt = now
    });

    var roomSpecs = new[]
    {
        (Id: roomIds[0], Number: "AB-101", Price: 3_500_000m, MaxOcc: 2),
        (Id: roomIds[1], Number: "AB-102", Price: 3_800_000m, MaxOcc: 2),
        (Id: roomIds[2], Number: "AB-103", Price: 5_000_000m, MaxOcc: 4),
        (Id: roomIds[3], Number: "AB-104", Price: 3_200_000m, MaxOcc: 2),
        (Id: roomIds[4], Number: "AB-105", Price: 4_000_000m, MaxOcc: 2)
    };
    db.Rooms.AddRange(roomSpecs.Select(r => new Room
    {
        Id = r.Id,
        PropertyId = propertyId,
        RoomNumber = r.Number,
        BasePrice = r.Price,
        MaxOccupants = r.MaxOcc,
        Status = RoomStatus.Occupied,
        CreatedAt = now
    }));

    var resolvedTenants = new[]
    {
        (UserId: resolvedT1UserId, ProfileId: resolvedT1ProfileId),
        (UserId: resolvedT2UserId, ProfileId: resolvedT2ProfileId),
        (UserId: resolvedT3UserId, ProfileId: resolvedT3ProfileId),
        (UserId: tenant4Id, ProfileId: profileIds[3])
    };

    var rulesId = Guid.NewGuid();
    db.ContractRulesVersions.Add(new ContractRulesVersion
    {
        Id = rulesId,
        PropertyId = propertyId,
        IsActive = true,
        VersionNumber = 1,
        Content = "1. Giữ gìn vệ sinh và an ninh trật tự chung.\n2. Thanh toán tiền phòng và điện nước trước ngày 05 hàng tháng.\n3. Không tự ý sửa chữa kết cấu phòng.",
        CreatedByUserId = landlordId,
        CreatedAt = now
    });

    // 5 signed Active contracts across 4 tenant accounts:
    // - tenant_1: HD-AB-101 & HD-AB-102 (1 account with multiple contracts)
    // - HD-AB-103: tenant_2 (rep) + tenant_3 + tenant_4 (1 contract with multiple accounts)
    // - tenant_3: HD-AB-104
    // - tenant_4: HD-AB-105
    var contractSpecs = new[]
    {
        (Id: contractIds[0], Code: "HD-AB-101", RoomIdx: 0, Mode: ContractTenancyMode.PrimaryTenantWithFamily, RepUserId: resolvedT1UserId, MemberIdxs: new[] { 0 }),
        (Id: contractIds[1], Code: "HD-AB-102", RoomIdx: 1, Mode: ContractTenancyMode.PrimaryTenantWithFamily, RepUserId: resolvedT1UserId, MemberIdxs: new[] { 0 }),
        (Id: contractIds[2], Code: "HD-AB-103", RoomIdx: 2, Mode: ContractTenancyMode.CoTenancy, RepUserId: resolvedT2UserId, MemberIdxs: new[] { 1, 2, 3 }),
        (Id: contractIds[3], Code: "HD-AB-104", RoomIdx: 3, Mode: ContractTenancyMode.PrimaryTenantWithFamily, RepUserId: resolvedT3UserId, MemberIdxs: new[] { 2 }),
        (Id: contractIds[4], Code: "HD-AB-105", RoomIdx: 4, Mode: ContractTenancyMode.PrimaryTenantWithFamily, RepUserId: tenant4Id, MemberIdxs: new[] { 3 })
    };

    var offerSentAt = now.AddDays(-62);
    var tenantConfirmedAt = now.AddDays(-61);
    var operatorConfirmedAt = now.AddDays(-60);

    foreach (var spec in contractSpecs)
    {
        var room = roomSpecs[spec.RoomIdx];
        var snapshotJson = JsonSerializer.Serialize(new
        {
            ContractId = spec.Id,
            ContractCode = spec.Code,
            PropertyId = propertyId,
            RoomId = room.Id,
            RoomNumber = room.Number,
            StartDate = baselineReadingMonth,
            EndDate = baselineReadingMonth.AddMonths(12),
            MonthlyRent = room.Price,
            DepositAmount = room.Price,
            PaymentDueDay = (byte)5,
            TenancyMode = spec.Mode.ToString(),
            RulesVersionId = rulesId
        });
        var termsHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshotJson))).ToLowerInvariant();

        db.Contracts.Add(new Contract
        {
            Id = spec.Id,
            ContractCode = spec.Code,
            RoomId = room.Id,
            StartDate = baselineReadingMonth,
            EndDate = baselineReadingMonth.AddMonths(12),
            MonthlyRent = room.Price,
            DepositAmount = room.Price,
            PaymentDueDay = 5,
            Status = ContractStatus.Active,
            TenancyMode = spec.Mode,
            OfferVersion = 1,
            OfferTermsHash = termsHash,
            OfferSnapshotJson = snapshotJson,
            OfferRulesVersionId = rulesId,
            OfferSentAt = offerSentAt,
            OfferSentByUserId = landlordId,
            TenantConfirmedAt = tenantConfirmedAt,
            TenantConfirmedByUserId = spec.RepUserId,
            TenantConfirmedOfferVersion = 1,
            TenantConfirmedTermsHash = termsHash,
            TenantConfirmedRulesVersionId = rulesId,
            OperatorConfirmedAt = operatorConfirmedAt,
            OperatorConfirmedByUserId = landlordId,
            Note = "Hợp đồng đã ký và kích hoạt để kiểm thử tạo hóa đơn hàng tháng.",
            CreatedAt = offerSentAt
        });

        for (var i = 0; i < spec.MemberIdxs.Length; i++)
        {
            var tenantSpec = resolvedTenants[spec.MemberIdxs[i]];
            var linkId = Guid.NewGuid();
            var isRep = i == 0;
            db.ContractTenants.Add(new ContractTenant
            {
                Id = linkId,
                ContractId = spec.Id,
                TenantId = tenantSpec.ProfileId,
                JoinDate = baselineReadingMonth,
                IsContactPerson = isRep,
                OnlineRulesRequired = true,
                Role = ContractTenantRole.ContractParty,
                CreatedAt = offerSentAt
            });
            db.ContractRulesAcceptances.Add(new ContractRulesAcceptance
            {
                Id = Guid.NewGuid(),
                RulesVersionId = rulesId,
                ContractTenantId = linkId,
                AcceptedByUserId = tenantSpec.UserId,
                AcceptedAt = tenantConfirmedAt
            });
        }

        db.ContractOfferEvents.AddRange(
            new ContractOfferEvent
            {
                Id = Guid.NewGuid(), ContractId = spec.Id, OfferVersion = 1, Action = "Sent",
                TermsHash = termsHash, RulesVersionId = rulesId, SnapshotJson = snapshotJson,
                ActorUserId = landlordId, CreatedAt = offerSentAt
            },
            new ContractOfferEvent
            {
                Id = Guid.NewGuid(), ContractId = spec.Id, OfferVersion = 1, Action = "TenantConfirmed",
                TermsHash = termsHash, RulesVersionId = rulesId, SnapshotJson = snapshotJson,
                ActorUserId = spec.RepUserId, CreatedAt = tenantConfirmedAt
            },
            new ContractOfferEvent
            {
                Id = Guid.NewGuid(), ContractId = spec.Id, OfferVersion = 1, Action = "OperatorCountersigned",
                TermsHash = termsHash, RulesVersionId = rulesId, SnapshotJson = snapshotJson,
                ActorUserId = landlordId, CreatedAt = operatorConfirmedAt
            }
        );
    }

    // Active utility rates effective before target billing period
    db.UtilityRates.AddRange(
        new UtilityRate
        {
            Id = Guid.NewGuid(), PropertyId = propertyId, UtilityType = UtilityType.Electricity,
            UnitPrice = 3_500, Unit = "kWh", EffectiveFrom = baselineReadingMonth, CreatedAt = now
        },
        new UtilityRate
        {
            Id = Guid.NewGuid(), PropertyId = propertyId, UtilityType = UtilityType.Water,
            UnitPrice = 15_000, Unit = "m³", EffectiveFrom = baselineReadingMonth, CreatedAt = now
        }
    );

    // Property services & full-period room service assignments
    var internetId = Guid.NewGuid();
    var parkingId = Guid.NewGuid();
    var trashId = Guid.NewGuid();
    db.Services.AddRange(
        new ServiceEntity { Id = internetId, PropertyId = propertyId, Name = "Internet cáp quang", Unit = "phòng/tháng", DefaultPrice = 100_000, CreatedAt = now },
        new ServiceEntity { Id = parkingId, PropertyId = propertyId, Name = "Giữ xe máy", Unit = "xe/tháng", DefaultPrice = 80_000, CreatedAt = now },
        new ServiceEntity { Id = trashId, PropertyId = propertyId, Name = "Vệ sinh & Rác", Unit = "phòng/tháng", DefaultPrice = 30_000, CreatedAt = now }
    );

    for (var i = 0; i < roomIds.Length; i++)
    {
        db.RoomServices.Add(new RoomService { Id = Guid.NewGuid(), RoomId = roomIds[i], ServiceId = internetId, Quantity = 1, StartDate = baselineReadingMonth, CreatedAt = now });
        db.RoomServices.Add(new RoomService { Id = Guid.NewGuid(), RoomId = roomIds[i], ServiceId = trashId, Quantity = 1, StartDate = baselineReadingMonth, CreatedAt = now });
        if (i is 1 or 2 or 4)
            db.RoomServices.Add(new RoomService { Id = Guid.NewGuid(), RoomId = roomIds[i], ServiceId = parkingId, Quantity = i == 2 ? 2 : 1, StartDate = baselineReadingMonth, CreatedAt = now });
    }

    // Baseline meter readings for baselineReadingMonth (targetBillingMonth - 1) so user can enter targetBillingMonth readings
    var baselineReadings = new (int RoomIdx, decimal ElecPrev, decimal ElecCurr, decimal WaterPrev, decimal WaterCurr)[]
    {
        (0, 100, 150, 10, 18),
        (1, 200, 265, 20, 29),
        (2, 300, 420, 30, 48),
        (3, 50, 95, 5, 11),
        (4, 120, 185, 15, 23)
    };
    foreach (var br in baselineReadings)
    {
        db.MeterReadings.AddRange(
            new MeterReading
            {
                Id = Guid.NewGuid(), RoomId = roomIds[br.RoomIdx], ContractId = null,
                MeterType = UtilityType.Electricity, Year = (short)baselineReadingMonth.Year, Month = (byte)baselineReadingMonth.Month,
                PreviousValue = br.ElecPrev, CurrentValue = br.ElecCurr, RecordedAt = now.AddDays(-30)
            },
            new MeterReading
            {
                Id = Guid.NewGuid(), RoomId = roomIds[br.RoomIdx], ContractId = null,
                MeterType = UtilityType.Water, Year = (short)baselineReadingMonth.Year, Month = (byte)baselineReadingMonth.Month,
                PreviousValue = br.WaterPrev, CurrentValue = br.WaterCurr, RecordedAt = now.AddDays(-30)
            }
        );
    }

    await db.SaveChangesAsync();
    await tx.CommitAsync();

    Console.WriteLine("Seeded auto-billing test data successfully!");
    Console.WriteLine($"Local password file:    {passwordPath}");
    Console.WriteLine($"PropertyId:             {propertyId}");
    Console.WriteLine($"Baseline Meter Month:   {baselineReadingMonth:yyyy-MM}");
    Console.WriteLine($"Target Billing Month:   {targetBillingMonth:yyyy-MM}");
    Console.WriteLine($"Tenant 1 (HD-AB-101, HD-AB-102): UserId={resolvedT1UserId}");
    Console.WriteLine($"Tenant 2 (HD-AB-103 rep):        UserId={resolvedT2UserId}");
    Console.WriteLine($"Tenant 3 (HD-AB-103, HD-AB-104): UserId={resolvedT3UserId}");
    Console.WriteLine($"Tenant 4 (HD-AB-103, HD-AB-105): UserId={tenant4Id}");
    return 0;
}

// Reset is restricted strictly to this fixture's property, profiles, contracts, and users.
await using (var tx = await db.Database.BeginTransactionAsync())
{
    var knownUsers = await db.Users.Where(u => fixtureUserIds.Contains(u.Id)).ToListAsync();
    if (knownUsers.Any(u => !u.UserName.StartsWith(prefix, StringComparison.Ordinal)))
        throw new InvalidOperationException("A reserved auto-billing user ID belongs to a non-fixture account. Reset aborted.");

    var propertyQuery = db.Properties.Where(p => p.Id == propertyId || p.OwnerUserId == landlordId).Select(p => p.Id);
    var roomQuery = db.Rooms.Where(r => propertyQuery.Contains(r.PropertyId)).Select(r => r.Id);
    var contractQuery = db.Contracts.Where(c => roomQuery.Contains(c.RoomId) || contractIds.Contains(c.Id)).Select(c => c.Id);
    var invoiceQuery = db.Invoices.Where(i => contractQuery.Contains(i.ContractId)).Select(i => i.Id);
    var paymentQuery = db.Payments.Where(p => invoiceQuery.Contains(p.InvoiceId)).Select(p => p.Id);
    var profileQuery = db.TenantProfiles.Where(t => profileIds.Contains(t.Id) || t.CreatedByUserId == landlordId || (t.UserId != null && fixtureUserIds.Contains(t.UserId))).Select(t => t.Id);
    var contractTenantQuery = db.ContractTenants.Where(t => contractQuery.Contains(t.ContractId)).Select(t => t.Id);
    var rulesQuery = db.ContractRulesVersions.Where(r => (r.PropertyId != null && propertyQuery.Contains(r.PropertyId.Value)) || (r.ContractId != null && contractQuery.Contains(r.ContractId.Value))).Select(r => r.Id);
    var acceptanceQuery = db.ContractRulesAcceptances.Where(a => rulesQuery.Contains(a.RulesVersionId) || contractTenantQuery.Contains(a.ContractTenantId)).Select(a => a.Id);

    var blobNames = await db.RoomImages.Where(x => roomQuery.Contains(x.RoomId)).Select(x => x.BlobName)
        .Concat(db.PaymentEvidence.Where(x => paymentQuery.Contains(x.PaymentId)).Select(x => x.BlobName))
        .Concat(db.ContractRulesEvidence.Where(x => acceptanceQuery.Contains(x.AcceptanceId)).Select(x => x.BlobName))
        .Distinct().ToListAsync();
    var now = DateTime.UtcNow;
    foreach (var blob in blobNames)
    {
        if (!await db.BlobCleanupJobs.AnyAsync(j => j.BlobName == blob))
            db.BlobCleanupJobs.Add(new BlobCleanupJob { Id = Guid.NewGuid(), BlobName = blob, CreatedAt = now, NextAttemptAt = now });
    }
    await db.SaveChangesAsync();

    await db.UserNotifications.Where(x => fixtureUserIds.Contains(x.UserId) || (x.ContractId != null && contractQuery.Contains(x.ContractId.Value))).ExecuteDeleteAsync();
    await db.TenantLookupAudits.Where(x => fixtureUserIds.Contains(x.ActorUserId) || fixtureUserIds.Contains(x.OwnerLandlordUserId)).ExecuteDeleteAsync();
    await db.ContractOfferEvents.Where(x => contractQuery.Contains(x.ContractId)).ExecuteDeleteAsync();
    await db.ContractRulesEvidence.Where(x => acceptanceQuery.Contains(x.AcceptanceId)).ExecuteDeleteAsync();
    await db.ContractRulesAcceptances.Where(x => acceptanceQuery.Contains(x.Id)).ExecuteDeleteAsync();
    await db.MemberCommitments.Where(x => contractTenantQuery.Contains(x.ContractTenantId) || (x.SignerTenantProfileId != null && profileQuery.Contains(x.SignerTenantProfileId.Value))).ExecuteDeleteAsync();
    var requestQuery = db.Requests
        .Where(r => propertyQuery.Contains(r.PropertyId)
            || (r.ContractId != null && contractQuery.Contains(r.ContractId.Value))
            || fixtureUserIds.Contains(r.SenderUserId))
        .Select(r => r.Id);
    await db.RequestRecipients.Where(rr => requestQuery.Contains(rr.RequestId) || fixtureUserIds.Contains(rr.ReceiverUserId)).ExecuteDeleteAsync();
    await db.Requests.Where(r => requestQuery.Contains(r.Id)).ExecuteDeleteAsync();
    await db.TenantRequests.Where(x => contractQuery.Contains(x.ContractId)).ExecuteDeleteAsync();
    await db.PaymentEvidence.Where(x => paymentQuery.Contains(x.PaymentId)).ExecuteDeleteAsync();
    await db.PaymentReviewEvents.Where(x => paymentQuery.Contains(x.PaymentId)).ExecuteDeleteAsync();
    await db.Payments.Where(x => paymentQuery.Contains(x.Id)).ExecuteDeleteAsync();
    await db.Refunds.Where(x => invoiceQuery.Contains(x.InvoiceId)).ExecuteDeleteAsync();
    await db.DepositApplications.Where(x => contractQuery.Contains(x.ContractId)).ExecuteDeleteAsync();
    await db.InvoiceAdjustments.Where(x => invoiceQuery.Contains(x.InvoiceId)).ExecuteDeleteAsync();
    await db.InvoiceItems.Where(x => invoiceQuery.Contains(x.InvoiceId)).ExecuteDeleteAsync();
    await db.Invoices.Where(x => invoiceQuery.Contains(x.Id)).ExecuteDeleteAsync();
    await db.ContractSettlements.Where(x => contractQuery.Contains(x.ContractId)).ExecuteDeleteAsync();
    await db.ContractDocuments.Where(x => contractQuery.Contains(x.ContractId)).ExecuteDeleteAsync();
    await db.MeterReadings.Where(x => roomQuery.Contains(x.RoomId) || (x.ContractId != null && contractQuery.Contains(x.ContractId.Value))).ExecuteDeleteAsync();
    await db.ContractTenants.Where(x => contractTenantQuery.Contains(x.Id)).ExecuteDeleteAsync();
    await db.ContractRulesVersions.Where(x => rulesQuery.Contains(x.Id)).ExecuteDeleteAsync();
    await db.Contracts.Where(x => contractQuery.Contains(x.Id)).ExecuteDeleteAsync();
    await db.RoomServices.Where(x => roomQuery.Contains(x.RoomId)).ExecuteDeleteAsync();
    await db.RoomImages.Where(x => roomQuery.Contains(x.RoomId)).ExecuteDeleteAsync();
    await db.RoomBatchSubmissions.Where(x => propertyQuery.Contains(x.PropertyId)).ExecuteDeleteAsync();
    await db.RoomNumberCounters.Where(x => propertyQuery.Contains(x.PropertyId)).ExecuteDeleteAsync();
    await db.Rooms.Where(x => roomQuery.Contains(x.Id)).ExecuteDeleteAsync();
    await db.RoomRows.Where(x => propertyQuery.Contains(x.PropertyId)).ExecuteDeleteAsync();
    await db.UtilityRates.Where(x => propertyQuery.Contains(x.PropertyId)).ExecuteDeleteAsync();
    await db.Services.Where(x => x.PropertyId != null && propertyQuery.Contains(x.PropertyId.Value)).ExecuteDeleteAsync();
    await db.PropertyManagerAssignments.Where(x => propertyQuery.Contains(x.PropertyId) || fixtureUserIds.Contains(x.ManagerUserId)).ExecuteDeleteAsync();
    await db.Properties.Where(x => propertyQuery.Contains(x.Id)).ExecuteDeleteAsync();
    await db.TenantPhoneChallenges.Where(x => fixtureUserIds.Contains(x.LandlordId)).ExecuteDeleteAsync();
    await db.TenantProfiles.Where(x => profileQuery.Contains(x.Id)).ExecuteDeleteAsync();
    await db.Users.Where(x => fixtureUserIds.Contains(x.Id)).ExecuteDeleteAsync();
    await tx.CommitAsync();

    Console.WriteLine($"Reset complete. {blobNames.Count} uploaded blobs queued for local API cleanup.");
}
return 0;
