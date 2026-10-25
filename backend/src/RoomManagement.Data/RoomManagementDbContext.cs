using Microsoft.EntityFrameworkCore;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Data;

public sealed class RoomManagementDbContext : DbContext
{
    public RoomManagementDbContext(DbContextOptions<RoomManagementDbContext> options)
        : base(options)
    {
    }

    public DbSet<AppUser> Users => Set<AppUser>();
public DbSet<TenantProfile> TenantProfiles => Set<TenantProfile>();
public DbSet<Property> Properties => Set<Property>();
public DbSet<Room> Rooms => Set<Room>();
public DbSet<Service> Services => Set<Service>();
public DbSet<RoomService> RoomServices => Set<RoomService>();
public DbSet<UtilityRate> UtilityRates => Set<UtilityRate>();
public DbSet<MeterReading> MeterReadings => Set<MeterReading>();
public DbSet<Contract> Contracts => Set<Contract>();
public DbSet<ContractTenant> ContractTenants => Set<ContractTenant>();
public DbSet<ContractDocument> ContractDocuments => Set<ContractDocument>();
public DbSet<Invoice> Invoices => Set<Invoice>();
public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
public DbSet<InvoiceAdjustment> InvoiceAdjustments => Set<InvoiceAdjustment>();
public DbSet<Payment> Payments => Set<Payment>();
public DbSet<PaymentReviewEvent> PaymentReviewEvents => Set<PaymentReviewEvent>();
public DbSet<Refund> Refunds => Set<Refund>();
public DbSet<DepositApplication> DepositApplications => Set<DepositApplication>();
public DbSet<ContractSettlement> ContractSettlements => Set<ContractSettlement>();
public DbSet<TenantRequest> TenantRequests => Set<TenantRequest>();
public DbSet<InvoiceBalanceRow> InvoiceBalances => Set<InvoiceBalanceRow>();
public DbSet<ContractDepositBalanceRow> ContractDepositBalances => Set<ContractDepositBalanceRow>();

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
  modelBuilder.ApplyConfigurationsFromAssembly(typeof(RoomManagementDbContext).Assembly);

  modelBuilder.Entity<InvoiceBalanceRow>(entity =>
  {
    entity.HasNoKey().ToView("vwInvoiceBalances", "dbo");
    entity.Property(e => e.InvoiceType).HasColumnType("nvarchar(20)");
    entity.Property(e => e.Status).HasColumnType("nvarchar(20)");
    entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
    entity.Property(e => e.TotalAdjustments).HasColumnType("decimal(18,2)");
    entity.Property(e => e.TotalPaid).HasColumnType("decimal(18,2)");
    entity.Property(e => e.TotalRefunded).HasColumnType("decimal(18,2)");
    entity.Property(e => e.TotalDepositApplied).HasColumnType("decimal(18,2)");
    entity.Property(e => e.EffectiveAmount).HasColumnType("decimal(18,2)");
    entity.Property(e => e.RemainingAmount).HasColumnType("decimal(18,2)");
  });
  modelBuilder.Entity<ContractDepositBalanceRow>(entity =>
  {
    entity.HasNoKey().ToView("vwContractDepositBalances", "dbo");
    entity.Property(e => e.DepositCollected).HasColumnType("decimal(18,2)");
    entity.Property(e => e.DepositRefunded).HasColumnType("decimal(18,2)");
    entity.Property(e => e.DepositApplied).HasColumnType("decimal(18,2)");
    entity.Property(e => e.DepositHeld).HasColumnType("decimal(18,2)");
  });
}
}
