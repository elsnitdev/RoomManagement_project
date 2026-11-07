using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Data.Configurations;

public sealed class MemberCommitmentConfiguration : IEntityTypeConfiguration<MemberCommitment>
{
    public void Configure(EntityTypeBuilder<MemberCommitment> builder)
    {
        builder.ToTable("MemberCommitments", "dbo", table =>
        {
            table.HasCheckConstraint("CK_MemberCommitments_Type", "CommitmentType IN (N'RoomRulesAgreement')");
            table.HasCheckConstraint("CK_MemberCommitments_Evidence", "EvidenceStatus IN (N'PendingUpload', N'Uploaded', N'LegacyPendingEvidence')");
            table.HasCheckConstraint("CK_MemberCommitments_Confirmation", "(EvidenceStatus = N'LegacyPendingEvidence' AND ConfirmedAt IS NULL AND ConfirmedByUserId IS NULL AND SignedOn IS NULL) OR (EvidenceStatus <> N'LegacyPendingEvidence' AND ConfirmedAt IS NOT NULL AND ConfirmedByUserId IS NOT NULL AND SignedOn IS NOT NULL AND SignerName IS NOT NULL AND TermsSnapshot IS NOT NULL)");
            table.HasCheckConstraint("CK_MemberCommitments_Superseded", "SupersededAt IS NULL OR ConfirmedAt IS NULL OR SupersededAt >= ConfirmedAt");
        });
        builder.HasKey(x => x.Id).HasName("PK_MemberCommitments");
        builder.Property(x => x.Id).HasColumnType("uniqueidentifier").ValueGeneratedNever();
        builder.Property(x => x.ContractTenantId).HasColumnType("uniqueidentifier").IsRequired();
        builder.Property(x => x.CommitmentType).HasConversion<string>().HasColumnType("nvarchar(30)").IsRequired();
        builder.Property(x => x.SignedOn).HasColumnType("date");
        builder.Property(x => x.SignerName).HasColumnType("nvarchar(200)");
        builder.Property(x => x.SignerTenantProfileId).HasColumnType("uniqueidentifier");
        builder.Property(x => x.SignerRelation).HasColumnType("nvarchar(100)");
        builder.Property(x => x.TermsSnapshot).HasColumnType("nvarchar(max)");
        builder.Property(x => x.ConfirmedAt).HasColumnType("datetime2(0)");
        builder.Property(x => x.ConfirmedByUserId).HasColumnType("nvarchar(450)");
        builder.Property(x => x.EvidenceStatus).HasConversion<string>().HasColumnType("nvarchar(32)").IsRequired();
        builder.Property(x => x.SupersededAt).HasColumnType("datetime2(0)");
        builder.Property(x => x.CreatedAt).HasColumnType("datetime2(0)").IsRequired();
        builder.HasOne<ContractTenant>().WithMany().HasForeignKey(x => x.ContractTenantId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_MemberCommitments_ContractTenants");
        builder.HasOne<TenantProfile>().WithMany().HasForeignKey(x => x.SignerTenantProfileId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_MemberCommitments_SignerTenantProfiles");
        builder.HasOne<AppUser>().WithMany().HasForeignKey(x => x.ConfirmedByUserId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_MemberCommitments_ConfirmedBy");
        builder.HasIndex(x => new { x.ContractTenantId, x.CommitmentType })
            .HasDatabaseName("UX_MemberCommitments_Current").IsUnique().HasFilter("SupersededAt IS NULL");
        builder.HasIndex(x => x.SignerTenantProfileId).HasDatabaseName("IX_MemberCommitments_SignerTenantProfileId");
        builder.HasIndex(x => x.ConfirmedByUserId).HasDatabaseName("IX_MemberCommitments_ConfirmedByUserId");
    }
}
