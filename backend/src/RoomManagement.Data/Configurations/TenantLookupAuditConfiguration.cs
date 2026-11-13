using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Data.Configurations;

public sealed class TenantLookupAuditConfiguration : IEntityTypeConfiguration<TenantLookupAudit>
{
    public void Configure(EntityTypeBuilder<TenantLookupAudit> builder)
    {
        builder.ToTable("TenantLookupAudits", "dbo");
        builder.HasKey(e => e.Id).HasName("PK_TenantLookupAudits");
        builder.Property(e => e.Id).HasColumnType("uniqueidentifier").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.ActorUserId).HasColumnType("nvarchar(450)").IsRequired();
        builder.Property(e => e.OwnerLandlordUserId).HasColumnType("nvarchar(450)").IsRequired();
        builder.Property(e => e.PropertyId).HasColumnType("uniqueidentifier");
        builder.Property(e => e.MatchedUserId).HasColumnType("nvarchar(450)");
        builder.Property(e => e.IsMatched).HasColumnType("bit").IsRequired();
        builder.Property(e => e.MaskedPhone).HasColumnType("varchar(30)").IsRequired();
        builder.Property(e => e.MaskedCitizenId).HasColumnType("varchar(30)").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").IsRequired().HasDefaultValueSql(@"SYSUTCDATETIME()", "DF_TenantLookupAudits_CreatedAt");

        builder.HasIndex(e => new { e.ActorUserId, e.CreatedAt }).HasDatabaseName("IX_TenantLookupAudits_Actor_CreatedAt");
    }
}
