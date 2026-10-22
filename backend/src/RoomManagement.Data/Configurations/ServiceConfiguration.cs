using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Data.Configurations;

public sealed class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> builder)
    {
        builder.ToTable("Services", "dbo", table =>
        {
            table.HasCheckConstraint("CK_Services_DefaultPrice", @"(DefaultPrice IS NULL OR DefaultPrice >= 0)");
        });
        builder.HasKey(e => e.Id).HasName("PK_Services");
        builder.Property(e => e.Id).HasColumnType("uniqueidentifier").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.Name).HasColumnType("nvarchar(100)").IsRequired();
        builder.Property(e => e.Description).HasColumnType("nvarchar(500)");
        builder.Property(e => e.Unit).HasColumnType("nvarchar(50)");
        builder.Property(e => e.DefaultPrice).HasColumnType("decimal(18,2)");
        builder.Property(e => e.IsActive).HasColumnType("bit").IsRequired().HasDefaultValueSql(@"1", "DF_Services_IsActive");
        builder.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").IsRequired().HasDefaultValueSql(@"SYSUTCDATETIME()", "DF_Services_CreatedAt");
        builder.HasIndex(e => e.Name).HasDatabaseName("UX_Services_Name").IsUnique();
    }
}
