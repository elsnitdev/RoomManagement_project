using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Data.Configurations;

public sealed class MeterReadingConfiguration : IEntityTypeConfiguration<MeterReading>
{
    public void Configure(EntityTypeBuilder<MeterReading> builder)
    {
        builder.ToTable("MeterReadings", "dbo", table =>
        {
            table.HasCheckConstraint("CK_MeterReadings_Type", @"(MeterType IN (N'Electricity', N'Water'))");
            table.HasCheckConstraint("CK_MeterReadings_Period", @"([Year] BETWEEN 2000 AND 2100 AND [Month] BETWEEN 1 AND 12)");
            table.HasCheckConstraint("CK_MeterReadings_Values", @"(PreviousValue >= 0 AND CurrentValue >= PreviousValue)");
        });
        builder.HasKey(e => e.Id).HasName("PK_MeterReadings");
        builder.Property(e => e.Id).HasColumnType("uniqueidentifier").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.RoomId).HasColumnType("uniqueidentifier").IsRequired();
        builder.Property(e => e.MeterType).HasColumnType("nvarchar(20)").IsRequired();
        builder.Property(e => e.Year).HasColumnType("smallint").IsRequired();
        builder.Property(e => e.Month).HasColumnType("tinyint").IsRequired();
        builder.Property(e => e.PreviousValue).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(e => e.CurrentValue).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(e => e.RecordedAt).HasColumnType("datetime2(0)").IsRequired().HasDefaultValueSql(@"SYSUTCDATETIME()", "DF_MeterReadings_RecordedAt");
        builder.Property(e => e.Note).HasColumnType("nvarchar(500)");
        builder.HasOne<Room>().WithMany().HasForeignKey(e => e.RoomId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_MeterReadings_Rooms");
        builder.HasIndex(e => new { e.RoomId, e.MeterType, e.Year, e.Month }).HasDatabaseName("UX_MeterReadings_Period").IsUnique();
    }
}
