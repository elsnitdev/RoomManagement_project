using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Data.Configurations;

public sealed class RequestRecipientConfiguration : IEntityTypeConfiguration<RequestRecipient>
{
    public void Configure(EntityTypeBuilder<RequestRecipient> builder)
    {
        builder.ToTable("RequestRecipients", "dbo", table =>
        {
            table.HasCheckConstraint("CK_RequestRecipients_Status", "(Status IN (N'Open', N'InProgress', N'Resolved', N'Closed'))");
        });

        builder.HasKey(e => e.Id).HasName("PK_RequestRecipients");
        builder.Property(e => e.Id).HasColumnType("uniqueidentifier").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.RequestId).HasColumnType("uniqueidentifier").IsRequired();
        builder.Property(e => e.ReceiverUserId).HasColumnType("nvarchar(450)").HasMaxLength(450).IsRequired();
        builder.Property(e => e.IsRead).HasColumnType("bit").IsRequired()
            .HasDefaultValueSql("0", "DF_RequestRecipients_IsRead");
        builder.Property(e => e.ReadAt).HasColumnType("datetime2(0)");
        builder.Property(e => e.Status).HasConversion<string>().HasColumnType("nvarchar(20)").IsRequired()
            .HasDefaultValueSql("N'Open'", "DF_RequestRecipients_Status");

        builder.HasOne<Request>()
            .WithMany()
            .HasForeignKey(e => e.RequestId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_RequestRecipients_Requests");

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(e => e.ReceiverUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_RequestRecipients_Receiver");

        builder.HasIndex(e => new { e.RequestId, e.ReceiverUserId })
            .HasDatabaseName("IX_RequestRecipients_RequestId_ReceiverUserId")
            .IsUnique();

        builder.HasIndex(e => new { e.ReceiverUserId, e.Status, e.IsRead })
            .HasDatabaseName("IX_RequestRecipients_ReceiverUserId_Status_IsRead");
    }
}
