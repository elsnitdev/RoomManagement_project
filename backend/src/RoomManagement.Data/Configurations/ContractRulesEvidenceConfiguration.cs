using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Data.Configurations;

public sealed class ContractRulesEvidenceConfiguration : IEntityTypeConfiguration<ContractRulesEvidence>
{
    public void Configure(EntityTypeBuilder<ContractRulesEvidence> b)
    {
        b.ToTable("ContractRulesEvidence", "dbo", t => t.HasCheckConstraint("CK_ContractRulesEvidence_Size", "[SizeBytes] > 0"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.BlobName).HasMaxLength(500).IsRequired();
        b.Property(x => x.OriginalFileName).HasMaxLength(255).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        b.Property(x => x.UploadedByUserId).HasMaxLength(450).IsRequired();
        b.Property(x => x.CreatedAt).HasColumnType("datetime2(0)");
        b.HasOne<ContractRulesAcceptance>().WithMany().HasForeignKey(x => x.AcceptanceId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UploadedByUserId).OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(x => x.BlobName).IsUnique();
        b.HasIndex(x => x.AcceptanceId);
    }
}
