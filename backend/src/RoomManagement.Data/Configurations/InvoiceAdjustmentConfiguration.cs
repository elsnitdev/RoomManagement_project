using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Data.Configurations;

public sealed class InvoiceAdjustmentConfiguration : IEntityTypeConfiguration<InvoiceAdjustment>
{
    public void Configure(EntityTypeBuilder<InvoiceAdjustment> builder)
    {
        builder.ToTable("InvoiceAdjustments", "dbo", table =>
        {
            table.HasCheckConstraint("CK_InvoiceAdjustments_Type", @"(AdjustmentType IN (N'Discount', N'Correction', N'WriteOff'))");
            table.HasCheckConstraint("CK_InvoiceAdjustments_Amount", @"(Amount > 0)");
            table.HasCheckConstraint("CK_InvoiceAdjustments_Void", @"((VoidedAt IS NULL AND VoidReason IS NULL AND VoidedByUserId IS NULL) OR (VoidedAt IS NOT NULL AND VoidReason IS NOT NULL AND VoidedByUserId IS NOT NULL))");
        });
        builder.HasKey(e => e.Id).HasName("PK_InvoiceAdjustments");
        builder.Property(e => e.Id).HasColumnType("uniqueidentifier").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.InvoiceId).HasColumnType("uniqueidentifier").IsRequired();
        builder.Property(e => e.AdjustmentType).HasConversion<string>().HasColumnType("nvarchar(20)").IsRequired();
        builder.Property(e => e.Amount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(e => e.Reason).HasColumnType("nvarchar(500)").IsRequired();
        builder.Property(e => e.IdempotencyKey).HasColumnType("varchar(100)").IsRequired();
        builder.Property(e => e.CreatedByUserId).HasColumnType("nvarchar(450)").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").IsRequired().HasDefaultValueSql(@"SYSUTCDATETIME()", "DF_InvoiceAdjustments_CreatedAt");
        builder.Property(e => e.VoidedAt).HasColumnType("datetime2(0)");
        builder.Property(e => e.VoidReason).HasColumnType("nvarchar(500)");
        builder.Property(e => e.VoidedByUserId).HasColumnType("nvarchar(450)");
        builder.HasOne<Invoice>().WithMany().HasForeignKey(e => e.InvoiceId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_InvoiceAdjustments_Invoices");
        builder.HasOne<AppUser>().WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_InvoiceAdjustments_CreatedBy");
        builder.HasOne<AppUser>().WithMany().HasForeignKey(e => e.VoidedByUserId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_InvoiceAdjustments_VoidedBy");
        builder.HasIndex(e => new { e.InvoiceId, e.VoidedAt }).HasDatabaseName("IX_InvoiceAdjustments_InvoiceId");
        builder.HasIndex(e => e.IdempotencyKey).HasDatabaseName("UX_InvoiceAdjustments_IdempotencyKey").IsUnique();
    }
}
