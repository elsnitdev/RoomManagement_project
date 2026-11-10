using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Data.Configurations;

public sealed class ContractRulesVersionConfiguration : IEntityTypeConfiguration<ContractRulesVersion>
{
    public void Configure(EntityTypeBuilder<ContractRulesVersion> b)
    {
        b.ToTable("ContractRulesVersions", "dbo");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Content).HasColumnType("nvarchar(max)").IsRequired();
        b.Property(x => x.CreatedByUserId).HasMaxLength(450).IsRequired();
        b.Property(x => x.CreatedAt).HasColumnType("datetime2(0)");
        b.HasOne<Contract>().WithMany().HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(x => new { x.ContractId, x.VersionNumber }).IsUnique().HasFilter("[ContractId] IS NOT NULL");
        b.HasIndex(x => new { x.PropertyId, x.VersionNumber }).IsUnique().HasFilter("[PropertyId] IS NOT NULL");
        b.HasIndex(x => x.PropertyId).IsUnique().HasFilter("[PropertyId] IS NOT NULL AND [IsActive] = 1");
        b.ToTable(t => t.HasCheckConstraint("CK_ContractRulesVersion_Scope", "([ContractId] IS NULL AND [PropertyId] IS NOT NULL) OR ([ContractId] IS NOT NULL AND [PropertyId] IS NULL)"));
    }
}

public sealed class ContractRulesAcceptanceConfiguration : IEntityTypeConfiguration<ContractRulesAcceptance>
{
    public void Configure(EntityTypeBuilder<ContractRulesAcceptance> b)
    {
        b.ToTable("ContractRulesAcceptances", "dbo");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.AcceptedByUserId).HasMaxLength(450).IsRequired();
        b.Property(x => x.AcceptedAt).HasColumnType("datetime2(0)");
        b.HasOne<ContractRulesVersion>().WithMany().HasForeignKey(x => x.RulesVersionId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<ContractTenant>().WithMany().HasForeignKey(x => x.ContractTenantId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.AcceptedByUserId).OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(x => new { x.RulesVersionId, x.ContractTenantId }).IsUnique();
    }
}
