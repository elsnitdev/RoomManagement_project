using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Data.Configurations;

public sealed class InvoiceItemConfiguration : IEntityTypeConfiguration<InvoiceItem>
{
    public void Configure(EntityTypeBuilder<InvoiceItem> builder)
    {
        builder.ToTable("InvoiceItems", "dbo", table =>
        {
            table.HasCheckConstraint("CK_InvoiceItems_Type", @"(ItemType IN (N'Rent', N'Deposit', N'Electricity', N'Water', N'Service', N'Damage', N'Penalty', N'Discount', N'Other'))");
            table.HasCheckConstraint("CK_InvoiceItems_Amounts", @"(Quantity > 0 AND UnitPrice >= 0 AND Amount = CASE WHEN ItemType = N'Discount' THEN -CONVERT(decimal(18,2), ROUND(Quantity * UnitPrice, 2)) ELSE CONVERT(decimal(18,2), ROUND(Quantity * UnitPrice, 2)) END)");
        });
        builder.HasKey(e => e.Id).HasName("PK_InvoiceItems");
        builder.Property(e => e.Id).HasColumnType("uniqueidentifier").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.InvoiceId).HasColumnType("uniqueidentifier").IsRequired();
        builder.Property(e => e.ItemType).HasConversion<string>().HasColumnType("nvarchar(20)").IsRequired();
        builder.Property(e => e.Description).HasColumnType("nvarchar(255)").IsRequired();
        builder.Property(e => e.Quantity).HasColumnType("decimal(18,2)").IsRequired().HasDefaultValueSql(@"1", "DF_InvoiceItems_Quantity");
        builder.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(e => e.Amount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(e => e.ReferenceId).HasColumnType("uniqueidentifier");
        builder.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").IsRequired().HasDefaultValueSql(@"SYSUTCDATETIME()", "DF_InvoiceItems_CreatedAt");
        builder.HasOne<Invoice>().WithMany().HasForeignKey(e => e.InvoiceId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_InvoiceItems_Invoices");
        builder.HasIndex(e => e.InvoiceId).HasDatabaseName("IX_InvoiceItems_InvoiceId");
    }
}
