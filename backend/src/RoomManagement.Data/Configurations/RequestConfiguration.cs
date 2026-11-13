using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Data.Configurations;

public sealed class RequestConfiguration : IEntityTypeConfiguration<Request>
{
    public void Configure(EntityTypeBuilder<Request> builder)
    {
        builder.ToTable("Requests", "dbo", table =>
        {
            table.HasCheckConstraint("CK_Requests_Type", "(Type IN (N'Request', N'Announcement'))");
            table.HasCheckConstraint("CK_Requests_RecipientType", "(RecipientType IN (N'Single', N'Multiple', N'All', N'Owner'))");
        });

        builder.HasKey(e => e.Id).HasName("PK_Requests");
        builder.Property(e => e.Id).HasColumnType("uniqueidentifier").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.PropertyId).HasColumnType("uniqueidentifier").IsRequired();
        builder.Property(e => e.SenderUserId).HasColumnType("nvarchar(450)").HasMaxLength(450).IsRequired();
        builder.Property(e => e.RecipientType).HasConversion<string>().HasColumnType("nvarchar(20)").IsRequired();
        builder.Property(e => e.Title).HasColumnType("nvarchar(250)").HasMaxLength(250).IsRequired();
        builder.Property(e => e.Content).HasColumnType("nvarchar(4000)").HasMaxLength(4000).IsRequired();
        builder.Property(e => e.Type).HasConversion<string>().HasColumnType("nvarchar(20)").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").IsRequired()
            .HasDefaultValueSql("SYSUTCDATETIME()", "DF_Requests_CreatedAt");

        builder.HasOne<Property>()
            .WithMany()
            .HasForeignKey(e => e.PropertyId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Requests_Properties");

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(e => e.SenderUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Requests_Sender");

        builder.HasIndex(e => new { e.PropertyId, e.Type, e.CreatedAt })
            .HasDatabaseName("IX_Requests_PropertyId_Type_CreatedAt");

        builder.HasIndex(e => new { e.SenderUserId, e.Type, e.CreatedAt })
            .HasDatabaseName("IX_Requests_SenderUserId_Type_CreatedAt");
    }
}

