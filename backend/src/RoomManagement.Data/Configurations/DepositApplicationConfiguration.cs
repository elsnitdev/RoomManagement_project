using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Data.Configurations;

public sealed class DepositApplicationConfiguration : IEntityTypeConfiguration<DepositApplication>
{
    public void Configure(EntityTypeBuilder<DepositApplication> builder)
    {
        builder.ToTable("DepositApplications", "dbo", table =>
        {
            table.HasCheckConstraint("CK_DepositApplications_Amount", @"(Amount > 0)");
        });
        builder.HasKey(e => e.Id).HasName("PK_DepositApplications");
        builder.Property(e => e.Id).HasColumnType("uniqueidentifier").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.ContractId).HasColumnType("uniqueidentifier").IsRequired();
        builder.Property(e => e.TargetInvoiceId).HasColumnType("uniqueidentifier").IsRequired();
        builder.Property(e => e.Amount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(e => e.Reason).HasColumnType("nvarchar(500)").IsRequired();
        builder.Property(e => e.IdempotencyKey).HasColumnType("varchar(100)").IsRequired();
        builder.Property(e => e.AppliedByUserId).HasColumnType("nvarchar(450)").IsRequired();
        builder.Property(e => e.AppliedAt).HasColumnType("datetime2(0)").IsRequired().HasDefaultValueSql(@"SYSUTCDATETIME()", "DF_DepositApplications_AppliedAt");
        builder.HasOne<Contract>().WithMany().HasForeignKey(e => e.ContractId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_DepositApplications_Contracts");
        builder.HasOne<Invoice>().WithMany().HasForeignKey(e => new { e.TargetInvoiceId, e.ContractId }).HasPrincipalKey(e => new { e.Id, e.ContractId }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_DepositApplications_TargetInvoice");
        builder.HasOne<AppUser>().WithMany().HasForeignKey(e => e.AppliedByUserId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_DepositApplications_AppliedBy");
        builder.HasIndex(e => e.ContractId).HasDatabaseName("IX_DepositApplications_ContractId");
        builder.HasIndex(e => e.TargetInvoiceId).HasDatabaseName("IX_DepositApplications_TargetInvoiceId");
        builder.HasIndex(e => e.IdempotencyKey).HasDatabaseName("UX_DepositApplications_IdempotencyKey").IsUnique();
    }
}
