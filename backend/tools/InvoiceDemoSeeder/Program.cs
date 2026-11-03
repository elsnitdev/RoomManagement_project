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

// Run from the repository root. The local connection string is never printed.
var configPath = Path.GetFullPath("backend/src/RoomManagement.API/appsettings.Local.json");
using var config = JsonDocument.Parse(await File.ReadAllTextAsync(configPath));
var connection = config.RootElement.GetProperty("ConnectionStrings")
    .GetProperty("RoomManagement").GetString()
    ?? throw new InvalidOperationException("Missing RoomManagement connection string.");
var options = new DbContextOptionsBuilder<RoomManagementDbContext>()
    .UseSqlServer(connection).Options;
await using var db = new RoomManagementDbContext(options);
if (!string.Equals(db.Database.GetDbConnection().Database, "RoomManagement", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("Seeder only accepts the RoomManagement database.");
var pending = (await db.Database.GetPendingMigrationsAsync()).ToArray();
const string expectedMigration = "20261026152004_AddCreatedByUserIdToTenantProfiles";
if (pending.Any(migration => migration != expectedMigration))
    throw new InvalidOperationException("Unexpected pending migration. Review schema before seeding.");
if (pending.Length != 0)
    await db.Database.MigrateAsync();

var now = DateTime.UtcNow;
var today = DateOnly.FromDateTime(now.AddHours(7));
var first = new DateOnly(today.Year, today.Month, 1);
var nextMonth = first.AddMonths(1);
var lastNextMonth = nextMonth.AddMonths(1).AddDays(-1);
var suffix = $"{today.Year:D4}{today.Month:D2}{today.Day:D2}";
var names = new[] { $"invoice_admin_{suffix}", $"invoice_landlord_{suffix}",
    $"invoice_tenant_a_{suffix}", $"invoice_tenant_b_{suffix}" };
if (await db.Users.AnyAsync(u => names.Contains(u.UserName)))
    throw new InvalidOperationException("Demo accounts already exist. The seeder does not reset passwords or modify existing data.");

var hasher = new PasswordHasher<AppUser>();
var passwords = names.Select(_ => Convert.ToBase64String(RandomNumberGenerator.GetBytes(18))
    .Replace('+', 'A').Replace('/', 'B') + "!a1").ToArray();
AppUser User(int index, string role, string fullName)
{
    var user = new AppUser
    {
        Id = Guid.NewGuid().ToString(), UserName = names[index],
        NormalizedUserName = names[index].ToUpperInvariant(),
        Role = role, IsActive = true, ApprovalStatus = ApprovalStatuses.Approved,
        FullName = fullName, CreatedAt = now, ReviewedAt = now
    };
    user.PasswordHash = hasher.HashPassword(user, passwords[index]);
    return user;
}

var admin = User(0, UserRoles.Admin, "Quản trị Invoice Demo");
var landlord = User(1, UserRoles.Landlord, "Chủ trọ Invoice Demo");
var tenantA = User(2, UserRoles.Tenant, "Người thuê A Invoice Demo");
var tenantB = User(3, UserRoles.Tenant, "Người thuê B Invoice Demo");
var propertyId = Guid.NewGuid();
var roomA = Guid.NewGuid();
var roomB = Guid.NewGuid();
var contractA = Guid.NewGuid();
var contractB = Guid.NewGuid();
var profileA = Guid.NewGuid();
var profileB = Guid.NewGuid();
var serviceId = Guid.NewGuid();

await using var tx = await db.Database.BeginTransactionAsync();
db.Users.AddRange(admin, landlord, tenantA, tenantB);
db.Properties.Add(new Property { Id = propertyId, OwnerUserId = landlord.Id,
    Name = $"Invoice Demo {suffix}", Address = "Dữ liệu kiểm thử cục bộ", IsActive = true, CreatedAt = now });
db.Rooms.AddRange(
    new Room { Id = roomA, PropertyId = propertyId, RoomNumber = "INV-A", BasePrice = 3_000_000,
        MaxOccupants = 2, Status = RoomStatus.Occupied, CreatedAt = now },
    new Room { Id = roomB, PropertyId = propertyId, RoomNumber = "INV-B", BasePrice = 2_500_000,
        MaxOccupants = 2, Status = RoomStatus.Occupied, CreatedAt = now });
db.TenantProfiles.AddRange(
    new TenantProfile { Id = profileA, UserId = tenantA.Id, CreatedByUserId = landlord.Id,
        FullName = tenantA.FullName!, PhoneNumber = "0900000101", CreatedAt = now },
    new TenantProfile { Id = profileB, UserId = tenantB.Id, CreatedByUserId = landlord.Id,
        FullName = tenantB.FullName!, PhoneNumber = "0900000102", CreatedAt = now });
db.Contracts.AddRange(
    new Contract { Id = contractA, ContractCode = $"INV-A-{suffix}", RoomId = roomA,
        StartDate = first, MonthlyRent = 3_000_000, DepositAmount = 1_000_000,
        PaymentDueDay = 5, Status = ContractStatus.Active, CreatedAt = now },
    new Contract { Id = contractB, ContractCode = $"INV-B-{suffix}", RoomId = roomB,
        StartDate = first, MonthlyRent = 2_500_000, DepositAmount = 0,
        PaymentDueDay = 5, Status = ContractStatus.Active, CreatedAt = now });
db.ContractTenants.AddRange(
    new ContractTenant { Id = Guid.NewGuid(), ContractId = contractA, TenantId = profileA,
        JoinDate = first, CreatedAt = now },
    new ContractTenant { Id = Guid.NewGuid(), ContractId = contractB, TenantId = profileB,
        JoinDate = first, CreatedAt = now });
db.UtilityRates.AddRange(
    new UtilityRate { Id = Guid.NewGuid(), PropertyId = propertyId,
        UtilityType = UtilityType.Electricity, UnitPrice = 3_500, Unit = "kWh",
        EffectiveFrom = first, EffectiveTo = lastNextMonth, CreatedAt = now },
    new UtilityRate { Id = Guid.NewGuid(), PropertyId = propertyId,
        UtilityType = UtilityType.Water, UnitPrice = 15_000, Unit = "m³",
        EffectiveFrom = first, EffectiveTo = lastNextMonth, CreatedAt = now });
db.MeterReadings.AddRange(
    new MeterReading { Id = Guid.NewGuid(), RoomId = roomA, MeterType = UtilityType.Electricity,
        Year = (short)first.Year, Month = (byte)first.Month, PreviousValue = 100, CurrentValue = 180, RecordedAt = now },
    new MeterReading { Id = Guid.NewGuid(), RoomId = roomA, MeterType = UtilityType.Water,
        Year = (short)first.Year, Month = (byte)first.Month, PreviousValue = 20, CurrentValue = 32, RecordedAt = now });
db.Services.Add(new ServiceEntity { Id = serviceId, Name = $"Internet Invoice Demo {suffix}",
    Description = "Dịch vụ dùng cho kiểm thử hóa đơn", Unit = "tháng",
    DefaultPrice = 100_000, IsActive = true, CreatedAt = now });
db.RoomServices.Add(new RoomManagement.Domain.Entities.RoomService { Id = Guid.NewGuid(),
    RoomId = roomA, ServiceId = serviceId, StartDate = first,
    EndDate = first.AddMonths(1).AddDays(-1), CreatedAt = now });
await db.SaveChangesAsync();
await tx.CommitAsync();

Console.WriteLine($"Database: {db.Database.GetDbConnection().Database}");
for (var i = 0; i < names.Length; i++)
    Console.WriteLine($"{names[i]} | {passwords[i]}");
Console.WriteLine($"PropertyId: {propertyId}");
Console.WriteLine($"RoomA: {roomA} | ContractA: {contractA} | TenantA: {profileA}");
Console.WriteLine($"RoomB: {roomB} | ContractB: {contractB} | TenantB: {profileB}");
Console.WriteLine($"BillingPeriod: {first:yyyy-MM}");
