using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Data.Configurations;

public sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.ToTable("Rooms", "dbo", table =>
        {
            table.HasCheckConstraint("CK_Rooms_Status", @"(Status IN (N'Available', N'Occupied', N'Maintenance', N'Inactive'))");
            table.HasCheckConstraint("CK_Rooms_Amounts", @"(BasePrice >= 0 AND MaxOccupants > 0 AND (Area IS NULL OR Area > 0))");
        });
        builder.HasKey(e => e.Id).HasName("PK_Rooms");
        builder.Property(e => e.Id).HasColumnType("uniqueidentifier").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.PropertyId).HasColumnType("uniqueidentifier").IsRequired();
        builder.Property(e => e.RoomNumber).HasColumnType("nvarchar(50)").IsRequired();
        builder.Property(e => e.RoomName).HasColumnType("nvarchar(100)");
        builder.Property(e => e.Floor).HasColumnType("int");
        builder.Property(e => e.Area).HasColumnType("decimal(10,2)");
        builder.Property(e => e.BasePrice).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(e => e.MaxOccupants).HasColumnType("int").IsRequired();
        builder.Property(e => e.Status).HasConversion<string>().HasColumnType("nvarchar(20)").IsRequired().HasDefaultValueSql(@"N'Available'", "DF_Rooms_Status");
        builder.Property(e => e.Description).HasColumnType("nvarchar(1000)");
        builder.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").IsRequired().HasDefaultValueSql(@"SYSUTCDATETIME()", "DF_Rooms_CreatedAt");
        builder.Property(e => e.UpdatedAt).HasColumnType("datetime2(0)");
        builder.HasOne<Property>().WithMany().HasForeignKey(e => e.PropertyId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Rooms_Properties");
        builder.HasIndex(e => new { e.PropertyId, e.RoomNumber }).HasDatabaseName("UX_Rooms_Property_Number").IsUnique();
        builder.HasIndex(e => new { e.PropertyId, e.Status }).HasDatabaseName("IX_Rooms_Property_Status");
    }
}
