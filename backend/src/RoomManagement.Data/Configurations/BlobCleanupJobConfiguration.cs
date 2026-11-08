using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Data.Configurations;

public sealed class BlobCleanupJobConfiguration : IEntityTypeConfiguration<BlobCleanupJob>
{
    public void Configure(EntityTypeBuilder<BlobCleanupJob> b)
    {
        b.ToTable("BlobCleanupJobs", "dbo");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.BlobName).HasMaxLength(500).IsRequired();
        b.Property(x => x.CreatedAt).HasColumnType("datetime2(0)");
        b.Property(x => x.NextAttemptAt).HasColumnType("datetime2(0)");
        b.HasIndex(x => x.NextAttemptAt);
    }
}
