using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Data.Configurations;

public sealed class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> builder)
    {
        builder.ToTable("Refunds", "dbo", table =>
        {
            table.HasCheckConstraint("CK_Refunds_Amount", @"(Amount > 0)");
            table.HasCheckConstraint("CK_Refunds_Status", @"(Status IN (N'Pending', N'Success', N'Failed', N'Cancelled'))");
            table.HasCheckConstraint("CK_Refunds_RefundedAt", @"(Status <> N'Success' OR RefundedAt IS NOT NULL)");
        });
        builder.HasKey(e => e.Id).HasName("PK_Refunds");
        builder.Property(e => e.Id).HasColumnType("uniqueidentifier").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.InvoiceId).HasColumnType("uniqueidentifier").IsRequired();
        builder.Property(e => e.Amount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(e => e.Status).HasConversion<string>().HasColumnType("nvarchar(20)").IsRequired();
        builder.Property(e => e.RefundedAt).HasColumnType("datetime2(0)");
        builder.Property(e => e.Reason).HasColumnType("nvarchar(500)").IsRequired();
        builder.Property(e => e.TransactionCode).HasColumnType("varchar(150)");
        builder.Property(e => e.IdempotencyKey).HasColumnType("varchar(100)").IsRequired();
        builder.Property(e => e.CreatedByUserId).HasColumnType("nvarchar(450)").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").IsRequired().HasDefaultValueSql(@"SYSUTCDATETIME()", "DF_Refunds_CreatedAt");
        builder.HasOne<Invoice>().WithMany().HasForeignKey(e => e.InvoiceId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Refunds_Invoices");
        builder.HasOne<AppUser>().WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Refunds_CreatedBy");
        builder.HasIndex(e => new { e.InvoiceId, e.Status }).HasDatabaseName("IX_Refunds_Invoice_Status");
        builder.HasIndex(e => e.TransactionCode).HasDatabaseName("UX_Refunds_TransactionCode").IsUnique().HasFilter(@"TransactionCode IS NOT NULL");
        builder.HasIndex(e => e.IdempotencyKey).HasDatabaseName("UX_Refunds_IdempotencyKey").IsUnique();
    }
}
