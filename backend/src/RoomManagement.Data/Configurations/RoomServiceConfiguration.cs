using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Data.Configurations;

public sealed class RoomServiceConfiguration : IEntityTypeConfiguration<RoomService>
{
    public void Configure(EntityTypeBuilder<RoomService> builder)
    {
        builder.ToTable("RoomServices", "dbo", table =>
        {
            table.HasCheckConstraint("CK_RoomServices_Dates", @"(EndDate IS NULL OR EndDate >= StartDate)");
            table.HasCheckConstraint("CK_RoomServices_Price", @"(CustomPrice IS NULL OR CustomPrice >= 0)");
        });
        builder.HasKey(e => e.Id).HasName("PK_RoomServices");
        builder.Property(e => e.Id).HasColumnType("uniqueidentifier").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.RoomId).HasColumnType("uniqueidentifier").IsRequired();
        builder.Property(e => e.ServiceId).HasColumnType("uniqueidentifier").IsRequired();
        builder.Property(e => e.CustomPrice).HasColumnType("decimal(18,2)");
        builder.Property(e => e.StartDate).HasColumnType("date").IsRequired();
        builder.Property(e => e.EndDate).HasColumnType("date");
        builder.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").IsRequired().HasDefaultValueSql(@"SYSUTCDATETIME()", "DF_RoomServices_CreatedAt");
        builder.HasOne<Room>().WithMany().HasForeignKey(e => e.RoomId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_RoomServices_Rooms");
        builder.HasOne<Service>().WithMany().HasForeignKey(e => e.ServiceId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_RoomServices_Services");
        builder.HasIndex(e => new { e.RoomId, e.ServiceId, e.StartDate }).HasDatabaseName("UX_RoomServices_Start").IsUnique();
        builder.HasIndex(e => new { e.RoomId, e.ServiceId }).HasDatabaseName("UX_RoomServices_Open").IsUnique().HasFilter(@"EndDate IS NULL");
        builder.HasIndex(e => e.ServiceId).HasDatabaseName("IX_RoomServices_ServiceId");
    }
}
