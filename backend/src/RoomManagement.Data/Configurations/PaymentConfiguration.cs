using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Data.Configurations;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments", "dbo", table =>
        {
            table.HasCheckConstraint("CK_Payments_ClaimedAmount", @"(ClaimedAmount > 0)");
            table.HasCheckConstraint("CK_Payments_Method", @"(PaymentMethod IN (N'Cash', N'BankTransfer', N'OnlinePayment', N'Other'))");
            table.HasCheckConstraint("CK_Payments_Status", @"(Status IN (N'Pending', N'Success', N'Failed', N'Cancelled'))");
            table.HasCheckConstraint("CK_Payments_Verification", @"((Status = N'Success' AND VerifiedAmount IS NOT NULL AND VerifiedAmount > 0 AND PaidAt IS NOT NULL AND VerifiedAt IS NOT NULL AND VerifiedByUserId IS NOT NULL) OR (Status <> N'Success' AND VerifiedAmount IS NULL AND VerifiedAt IS NULL AND VerifiedByUserId IS NULL))");
        });
        builder.HasKey(e => e.Id).HasName("PK_Payments");
        builder.Property(e => e.Id).HasColumnType("uniqueidentifier").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.InvoiceId).HasColumnType("uniqueidentifier").IsRequired();
        builder.Property(e => e.ClaimedAmount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(e => e.VerifiedAmount).HasColumnType("decimal(18,2)");
        builder.Property(e => e.PaymentMethod).HasConversion<string>().HasColumnType("nvarchar(20)").IsRequired();
        builder.Property(e => e.Status).HasConversion<string>().HasColumnType("nvarchar(20)").IsRequired();
        builder.Property(e => e.PaidAt).HasColumnType("datetime2(0)");
        builder.Property(e => e.VerifiedAt).HasColumnType("datetime2(0)");
        builder.Property(e => e.VerifiedByUserId).HasColumnType("nvarchar(450)");
        builder.Property(e => e.TransactionCode).HasColumnType("varchar(150)");
        builder.Property(e => e.IdempotencyKey).HasColumnType("varchar(100)").IsRequired();
        builder.Property(e => e.Note).HasColumnType("nvarchar(500)");
        builder.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").IsRequired().HasDefaultValueSql(@"SYSUTCDATETIME()", "DF_Payments_CreatedAt");
        builder.HasOne<Invoice>().WithMany().HasForeignKey(e => e.InvoiceId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Payments_Invoices");
        builder.HasOne<AppUser>().WithMany().HasForeignKey(e => e.VerifiedByUserId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Payments_VerifiedBy");
        builder.HasIndex(e => new { e.InvoiceId, e.Status }).HasDatabaseName("IX_Payments_Invoice_Status");
        builder.HasIndex(e => e.TransactionCode).HasDatabaseName("UX_Payments_TransactionCode").IsUnique().HasFilter(@"TransactionCode IS NOT NULL");
        builder.HasIndex(e => e.IdempotencyKey).HasDatabaseName("UX_Payments_IdempotencyKey").IsUnique();
    }
}
