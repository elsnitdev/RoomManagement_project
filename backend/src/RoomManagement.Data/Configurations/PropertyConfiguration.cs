using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Data.Configurations;

public sealed class PropertyConfiguration : IEntityTypeConfiguration<Property>
{
    public void Configure(EntityTypeBuilder<Property> builder)
    {
        builder.ToTable("Properties", "dbo");
        builder.HasKey(e => e.Id).HasName("PK_Properties");
        builder.Property(e => e.Id).HasColumnType("uniqueidentifier").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.OwnerUserId).HasColumnType("nvarchar(450)").IsRequired();
        builder.Property(e => e.Name).HasColumnType("nvarchar(150)").IsRequired();
        builder.Property(e => e.Address).HasColumnType("nvarchar(300)").IsRequired();
        builder.Property(e => e.Description).HasColumnType("nvarchar(1000)");
        builder.Property(e => e.IsActive).HasColumnType("bit").IsRequired().HasDefaultValueSql(@"1", "DF_Properties_IsActive").HasSentinel(true);
        builder.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").IsRequired().HasDefaultValueSql(@"SYSUTCDATETIME()", "DF_Properties_CreatedAt");
        builder.Property(e => e.UpdatedAt).HasColumnType("datetime2(0)");
        builder.HasOne<AppUser>().WithMany().HasForeignKey(e => e.OwnerUserId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Properties_Owner");
        builder.HasIndex(e => e.OwnerUserId).HasDatabaseName("IX_Properties_OwnerUserId");
    }
}
