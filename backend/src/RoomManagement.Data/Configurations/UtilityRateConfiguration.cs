using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Data.Configurations;

public sealed class UtilityRateConfiguration : IEntityTypeConfiguration<UtilityRate>
{
    public void Configure(EntityTypeBuilder<UtilityRate> builder)
    {
        builder.ToTable("UtilityRates", "dbo", table =>
        {
            table.HasCheckConstraint("CK_UtilityRates_Type", @"(UtilityType IN (N'Electricity', N'Water'))");
            table.HasCheckConstraint("CK_UtilityRates_Price", @"(UnitPrice >= 0)");
            table.HasCheckConstraint("CK_UtilityRates_Dates", @"(EffectiveTo IS NULL OR EffectiveTo >= EffectiveFrom)");
        });
        builder.HasKey(e => e.Id).HasName("PK_UtilityRates");
        builder.Property(e => e.Id).HasColumnType("uniqueidentifier").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.PropertyId).HasColumnType("uniqueidentifier").IsRequired();
        builder.Property(e => e.UtilityType).HasColumnType("nvarchar(20)").IsRequired();
        builder.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(e => e.Unit).HasColumnType("nvarchar(50)").IsRequired();
        builder.Property(e => e.EffectiveFrom).HasColumnType("date").IsRequired();
        builder.Property(e => e.EffectiveTo).HasColumnType("date");
        builder.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").IsRequired().HasDefaultValueSql(@"SYSUTCDATETIME()", "DF_UtilityRates_CreatedAt");
        builder.HasOne<Property>().WithMany().HasForeignKey(e => e.PropertyId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_UtilityRates_Properties");
        builder.HasIndex(e => new { e.PropertyId, e.UtilityType, e.EffectiveFrom }).HasDatabaseName("UX_UtilityRates_Start").IsUnique();
        builder.HasIndex(e => new { e.PropertyId, e.UtilityType }).HasDatabaseName("UX_UtilityRates_Open").IsUnique().HasFilter(@"EffectiveTo IS NULL");
    }
}
