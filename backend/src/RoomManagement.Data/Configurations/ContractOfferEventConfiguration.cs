using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Data.Configurations;

public sealed class ContractOfferEventConfiguration : IEntityTypeConfiguration<ContractOfferEvent>
{
    public void Configure(EntityTypeBuilder<ContractOfferEvent> builder)
    {
        builder.ToTable("ContractOfferEvents", "dbo");
        builder.HasKey(e => e.Id).HasName("PK_ContractOfferEvents");
        builder.Property(e => e.Id).HasColumnType("uniqueidentifier").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.ContractId).HasColumnType("uniqueidentifier").IsRequired();
        builder.Property(e => e.OfferVersion).HasColumnType("int").IsRequired();
        builder.Property(e => e.Action).HasColumnType("nvarchar(40)").IsRequired();
        builder.Property(e => e.TermsHash).HasColumnType("varchar(128)").IsRequired();
        builder.Property(e => e.RulesVersionId).HasColumnType("uniqueidentifier").IsRequired();
        builder.Property(e => e.SnapshotJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(e => e.ActorUserId).HasColumnType("nvarchar(450)").IsRequired();
        builder.Property(e => e.ActorIdentityVerificationStatus).HasColumnType("nvarchar(20)");
        builder.Property(e => e.Note).HasColumnType("nvarchar(1000)");
        builder.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").IsRequired().HasDefaultValueSql(@"SYSUTCDATETIME()", "DF_ContractOfferEvents_CreatedAt");

        builder.HasOne<Contract>().WithMany().HasForeignKey(e => e.ContractId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_ContractOfferEvents_Contracts");
        builder.HasIndex(e => new { e.ContractId, e.OfferVersion, e.CreatedAt }).HasDatabaseName("IX_ContractOfferEvents_Contract");
    }
}
