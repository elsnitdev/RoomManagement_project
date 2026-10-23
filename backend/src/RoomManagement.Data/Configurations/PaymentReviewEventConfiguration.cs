using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Data.Configurations;

public sealed class PaymentReviewEventConfiguration : IEntityTypeConfiguration<PaymentReviewEvent>
{
    public void Configure(EntityTypeBuilder<PaymentReviewEvent> builder)
    {
        builder.ToTable("PaymentReviewEvents", "dbo", table =>
        {
            table.HasCheckConstraint("CK_PaymentReviewEvents_PreviousStatus", @"(PreviousStatus IN (N'Pending', N'Success', N'Failed', N'Cancelled'))");
            table.HasCheckConstraint("CK_PaymentReviewEvents_NewStatus", @"(NewStatus IN (N'Pending', N'Success', N'Failed', N'Cancelled'))");
            table.HasCheckConstraint("CK_PaymentReviewEvents_Amounts", @"((PreviousVerifiedAmount IS NULL OR PreviousVerifiedAmount > 0) AND (NewVerifiedAmount IS NULL OR NewVerifiedAmount > 0))");
        });
        builder.HasKey(e => e.Id).HasName("PK_PaymentReviewEvents");
        builder.Property(e => e.Id).HasColumnType("uniqueidentifier").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.PaymentId).HasColumnType("uniqueidentifier").IsRequired();
        builder.Property(e => e.PreviousStatus).HasConversion<string>().HasColumnType("nvarchar(20)").IsRequired();
        builder.Property(e => e.NewStatus).HasConversion<string>().HasColumnType("nvarchar(20)").IsRequired();
        builder.Property(e => e.PreviousVerifiedAmount).HasColumnType("decimal(18,2)");
        builder.Property(e => e.NewVerifiedAmount).HasColumnType("decimal(18,2)");
        builder.Property(e => e.Reason).HasColumnType("nvarchar(500)").IsRequired();
        builder.Property(e => e.ReviewedByUserId).HasColumnType("nvarchar(450)").IsRequired();
        builder.Property(e => e.ReviewedAt).HasColumnType("datetime2(0)").IsRequired().HasDefaultValueSql(@"SYSUTCDATETIME()", "DF_PaymentReviewEvents_ReviewedAt");
        builder.HasOne<Payment>().WithMany().HasForeignKey(e => e.PaymentId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_PaymentReviewEvents_Payments");
        builder.HasOne<AppUser>().WithMany().HasForeignKey(e => e.ReviewedByUserId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_PaymentReviewEvents_ReviewedBy");
        builder.HasIndex(e => new { e.PaymentId, e.ReviewedAt }).HasDatabaseName("IX_PaymentReviewEvents_Payment_ReviewedAt");
    }
}
