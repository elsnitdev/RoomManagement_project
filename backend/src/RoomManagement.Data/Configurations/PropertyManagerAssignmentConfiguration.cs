using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Data.Configurations;

public sealed class PropertyManagerAssignmentConfiguration : IEntityTypeConfiguration<PropertyManagerAssignment>
{
    public void Configure(EntityTypeBuilder<PropertyManagerAssignment> builder)
    {
        builder.ToTable("PropertyManagerAssignments", "dbo");
        builder.HasKey(e => e.Id).HasName("PK_PropertyManagerAssignments");
        builder.Property(e => e.Id).HasColumnType("uniqueidentifier").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.ManagerUserId).HasColumnType("nvarchar(450)").IsRequired();
        builder.Property(e => e.PropertyId).HasColumnType("uniqueidentifier").IsRequired();
        builder.Property(e => e.AssignedByUserId).HasColumnType("nvarchar(450)").IsRequired();
        builder.Property(e => e.AssignedAt).HasColumnType("datetime2(0)").IsRequired()
            .HasDefaultValueSql(@"SYSUTCDATETIME()", "DF_PropertyManagerAssignments_AssignedAt");
        builder.Property(e => e.RevokedAt).HasColumnType("datetime2(0)");
        builder.Property(e => e.RevokedByUserId).HasColumnType("nvarchar(450)");

        builder.HasOne<AppUser>().WithMany().HasForeignKey(e => e.ManagerUserId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_PropertyManagerAssignments_ManagerUser");
        builder.HasOne<Property>().WithMany().HasForeignKey(e => e.PropertyId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_PropertyManagerAssignments_Property");
        builder.HasOne<AppUser>().WithMany().HasForeignKey(e => e.AssignedByUserId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_PropertyManagerAssignments_AssignedBy");
        builder.HasOne<AppUser>().WithMany().HasForeignKey(e => e.RevokedByUserId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_PropertyManagerAssignments_RevokedBy");

        // Không có hai phân công đang hiệu lực cho cùng cặp Manager-Property
        builder.HasIndex(e => new { e.ManagerUserId, e.PropertyId })
            .HasDatabaseName("UX_PropertyManagerAssignments_Active")
            .IsUnique()
            .HasFilter("RevokedAt IS NULL");

        builder.HasIndex(e => e.PropertyId).HasDatabaseName("IX_PropertyManagerAssignments_PropertyId");
    }
}
