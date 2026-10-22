using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Data.Configurations;

public sealed class ContractSettlementConfiguration : IEntityTypeConfiguration<ContractSettlement>
{
    public void Configure(EntityTypeBuilder<ContractSettlement> builder)
    {
        builder.ToTable("ContractSettlements", "dbo", table =>
        {
            table.HasCheckConstraint("CK_ContractSettlements_Amounts", @"(DepositAmount >= 0 AND OutstandingAmount >= 0 AND DamageFee >= 0 AND OtherFee >= 0 AND RefundAmount >= 0)");
        });
        builder.HasKey(e => e.Id).HasName("PK_ContractSettlements");
        builder.Property(e => e.Id).HasColumnType("uniqueidentifier").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.ContractId).HasColumnType("uniqueidentifier").IsRequired();
        builder.Property(e => e.SettlementDate).HasColumnType("date").IsRequired();
        builder.Property(e => e.DepositAmount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(e => e.OutstandingAmount).HasColumnType("decimal(18,2)").IsRequired().HasDefaultValueSql(@"0", "DF_ContractSettlements_Outstanding");
        builder.Property(e => e.DamageFee).HasColumnType("decimal(18,2)").IsRequired().HasDefaultValueSql(@"0", "DF_ContractSettlements_Damage");
        builder.Property(e => e.OtherFee).HasColumnType("decimal(18,2)").IsRequired().HasDefaultValueSql(@"0", "DF_ContractSettlements_Other");
        builder.Property(e => e.RefundAmount).HasColumnType("decimal(18,2)").IsRequired().HasDefaultValueSql(@"0", "DF_ContractSettlements_Refund");
        builder.Property(e => e.Note).HasColumnType("nvarchar(1000)");
        builder.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").IsRequired().HasDefaultValueSql(@"SYSUTCDATETIME()", "DF_ContractSettlements_CreatedAt");
        builder.HasOne<Contract>().WithMany().HasForeignKey(e => e.ContractId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_ContractSettlements_Contracts");
        builder.HasIndex(e => e.ContractId).HasDatabaseName("UX_ContractSettlements_ContractId").IsUnique();
    }
}
