using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Data.Configurations;

public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices", "dbo", table =>
        {
            table.HasCheckConstraint("CK_Invoices_Type", @"(InvoiceType IN (N'Monthly', N'Deposit', N'Damage', N'Settlement', N'Other'))");
            table.HasCheckConstraint("CK_Invoices_Status", @"(Status IN (N'Draft', N'Issued', N'Cancelled'))");
            table.HasCheckConstraint("CK_Invoices_Period", @"((BillingYear IS NULL AND BillingMonth IS NULL) OR (BillingYear IS NOT NULL AND BillingMonth IS NOT NULL AND BillingYear BETWEEN 2000 AND 2100 AND BillingMonth BETWEEN 1 AND 12))");
            table.HasCheckConstraint("CK_Invoices_MonthlyPeriod", @"(InvoiceType <> N'Monthly' OR BillingYear IS NOT NULL)");
            table.HasCheckConstraint("CK_Invoices_Dates", @"(DueDate >= IssueDate)");
            table.HasCheckConstraint("CK_Invoices_Total", @"(TotalAmount >= 0)");
        });
        builder.HasKey(e => e.Id).HasName("PK_Invoices");
        builder.Property(e => e.Id).HasColumnType("uniqueidentifier").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.ContractId).HasColumnType("uniqueidentifier").IsRequired();
        builder.Property(e => e.RoomId).HasColumnType("uniqueidentifier").IsRequired();
        builder.Property(e => e.InvoiceCode).HasColumnType("varchar(50)").IsRequired();
        builder.Property(e => e.InvoiceType).HasConversion<string>().HasColumnType("nvarchar(20)").IsRequired();
        builder.Property(e => e.BillingYear).HasColumnType("smallint");
        builder.Property(e => e.BillingMonth).HasColumnType("tinyint");
        builder.Property(e => e.IssueDate).HasColumnType("date").IsRequired();
        builder.Property(e => e.DueDate).HasColumnType("date").IsRequired();
        builder.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(e => e.Status).HasConversion<string>().HasColumnType("nvarchar(20)").IsRequired().HasDefaultValueSql(@"N'Draft'", "DF_Invoices_Status");
        builder.Property(e => e.Note).HasColumnType("nvarchar(1000)");
        builder.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").IsRequired().HasDefaultValueSql(@"SYSUTCDATETIME()", "DF_Invoices_CreatedAt");
        builder.HasOne<Contract>().WithMany().HasForeignKey(e => new { e.ContractId, e.RoomId }).HasPrincipalKey(e => new { e.Id, e.RoomId }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Invoices_ContractRoom");
        builder.HasAlternateKey(e => new { e.Id, e.ContractId }).HasName("UQ_Invoices_Id_Contract");
        builder.HasIndex(e => e.InvoiceCode).HasDatabaseName("UX_Invoices_Code").IsUnique();
        builder.HasIndex(e => new { e.ContractId, e.BillingYear, e.BillingMonth }).HasDatabaseName("UX_Invoices_Monthly").IsUnique().HasFilter(@"InvoiceType = N'Monthly' AND Status <> N'Cancelled'");
        builder.HasIndex(e => e.ContractId).HasDatabaseName("UX_Invoices_Deposit").IsUnique().HasFilter(@"InvoiceType = N'Deposit' AND Status <> N'Cancelled'");
        builder.HasIndex(e => new { e.ContractId, e.Status }).HasDatabaseName("IX_Invoices_Contract_Status");
    }
}
