using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Data.Configurations;

public sealed class ContractTenantConfiguration : IEntityTypeConfiguration<ContractTenant>
{
    public void Configure(EntityTypeBuilder<ContractTenant> builder)
    {
        builder.ToTable("ContractTenants", "dbo", table =>
        {
            table.HasCheckConstraint("CK_ContractTenants_Dates", @"(LeaveDate IS NULL OR LeaveDate >= JoinDate)");
        });
        builder.HasKey(e => e.Id).HasName("PK_ContractTenants");
        builder.Property(e => e.Id).HasColumnType("uniqueidentifier").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.ContractId).HasColumnType("uniqueidentifier").IsRequired();
        builder.Property(e => e.TenantId).HasColumnType("uniqueidentifier").IsRequired();
        builder.Property(e => e.JoinDate).HasColumnType("date").IsRequired();
        builder.Property(e => e.LeaveDate).HasColumnType("date");
        builder.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").IsRequired().HasDefaultValueSql(@"SYSUTCDATETIME()", "DF_ContractTenants_CreatedAt");
        builder.HasOne<Contract>().WithMany().HasForeignKey(e => e.ContractId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_ContractTenants_Contracts");
        builder.HasOne<TenantProfile>().WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_ContractTenants_Tenants");
        builder.HasIndex(e => new { e.ContractId, e.TenantId, e.JoinDate }).HasDatabaseName("UX_ContractTenants_Join").IsUnique();
        builder.HasIndex(e => e.TenantId).HasDatabaseName("IX_ContractTenants_TenantId");
    }
}
