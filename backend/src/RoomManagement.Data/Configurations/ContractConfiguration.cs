using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Data.Configurations;

public sealed class ContractConfiguration : IEntityTypeConfiguration<Contract>
{
    public void Configure(EntityTypeBuilder<Contract> builder)
    {
        builder.ToTable("Contracts", "dbo", table =>
        {
            table.HasCheckConstraint("CK_Contracts_Status", @"(Status IN (N'Draft', N'Active', N'Expired', N'Terminated', N'Cancelled'))");
            table.HasCheckConstraint("CK_Contracts_Dates", @"(EndDate IS NULL OR EndDate >= StartDate)");
            table.HasCheckConstraint("CK_Contracts_Amounts", @"(MonthlyRent >= 0 AND DepositAmount >= 0)");
            table.HasCheckConstraint("CK_Contracts_DueDay", @"(PaymentDueDay IS NULL OR PaymentDueDay BETWEEN 1 AND 31)");
        });
        builder.HasKey(e => e.Id).HasName("PK_Contracts");
        builder.Property(e => e.Id).HasColumnType("uniqueidentifier").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.ContractCode).HasColumnType("varchar(50)").IsRequired();
        builder.Property(e => e.RoomId).HasColumnType("uniqueidentifier").IsRequired();
        builder.Property(e => e.StartDate).HasColumnType("date").IsRequired();
        builder.Property(e => e.EndDate).HasColumnType("date");
        builder.Property(e => e.MonthlyRent).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(e => e.DepositAmount).HasColumnType("decimal(18,2)").IsRequired().HasDefaultValueSql(@"0", "DF_Contracts_Deposit");
        builder.Property(e => e.PaymentDueDay).HasColumnType("tinyint");
        builder.Property(e => e.Status).HasConversion<string>().HasColumnType("nvarchar(20)").IsRequired().HasDefaultValueSql(@"N'Draft'", "DF_Contracts_Status");
        builder.Property(e => e.Note).HasColumnType("nvarchar(1000)");
        builder.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").IsRequired().HasDefaultValueSql(@"SYSUTCDATETIME()", "DF_Contracts_CreatedAt");
        builder.Property(e => e.UpdatedAt).HasColumnType("datetime2(0)");
        builder.HasOne<Room>().WithMany().HasForeignKey(e => e.RoomId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Contracts_Rooms");
        builder.HasAlternateKey(e => new { e.Id, e.RoomId }).HasName("UQ_Contracts_Id_Room");
        builder.HasIndex(e => e.ContractCode).HasDatabaseName("UX_Contracts_Code").IsUnique();
        builder.HasIndex(e => e.RoomId).HasDatabaseName("UX_Contracts_OneActivePerRoom").IsUnique().HasFilter(@"Status = N'Active'");
        builder.HasIndex(e => new { e.RoomId, e.StartDate, e.EndDate }).HasDatabaseName("IX_Contracts_Room_Dates");
    }
}
