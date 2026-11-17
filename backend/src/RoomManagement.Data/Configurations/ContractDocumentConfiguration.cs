using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Data.Configurations;

public sealed class ContractDocumentConfiguration : IEntityTypeConfiguration<ContractDocument>
{
    public void Configure(EntityTypeBuilder<ContractDocument> builder)
    {
        builder.ToTable("ContractDocuments", "dbo", table =>
        {
            table.HasCheckConstraint("CK_ContractDocuments_Type", @"(DocumentType IN (N'ContractPage', N'CitizenId', N'Handover', N'Appendix', N'Other'))");
        });
        builder.HasKey(e => e.Id).HasName("PK_ContractDocuments");
        builder.Property(e => e.Id).HasColumnType("uniqueidentifier").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.ContractId).HasColumnType("uniqueidentifier").IsRequired();
        builder.Property(e => e.FileName).HasColumnType("nvarchar(255)").IsRequired();
        builder.Property(e => e.StorageKey).HasColumnType("nvarchar(500)").IsRequired();
        builder.Property(e => e.FileType).HasColumnType("nvarchar(100)");
        builder.Property(e => e.DocumentType).HasConversion<string>().HasColumnType("nvarchar(20)").IsRequired();
        builder.Property(e => e.OfferVersion).HasColumnType("int");
        builder.Property(e => e.PageOrder).HasColumnType("int");
        builder.Property(e => e.SizeBytes).HasColumnType("bigint");
        builder.Property(e => e.Sha256).HasColumnType("char(64)");
        builder.Property(e => e.UploadedByUserId).HasColumnType("nvarchar(450)");
        builder.Property(e => e.UploadedAt).HasColumnType("datetime2(0)").IsRequired().HasDefaultValueSql(@"SYSUTCDATETIME()", "DF_ContractDocuments_UploadedAt");
        builder.HasOne<Contract>().WithMany().HasForeignKey(e => e.ContractId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_ContractDocuments_Contracts");
        builder.HasIndex(e => e.ContractId).HasDatabaseName("IX_ContractDocuments_ContractId");
        builder.HasIndex(e => new { e.ContractId, e.OfferVersion, e.PageOrder }).HasDatabaseName("IX_ContractDocuments_OfferPages");
    }
}
