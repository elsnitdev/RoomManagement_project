using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Data.Configurations;

public sealed class TenantPhoneChallengeConfiguration : IEntityTypeConfiguration<TenantPhoneChallenge>
{
    public void Configure(EntityTypeBuilder<TenantPhoneChallenge> b)
    {
        b.ToTable("TenantPhoneChallenges", "dbo");
        b.HasKey(x => x.Id);
        b.Property(x => x.LandlordId).HasMaxLength(450).IsRequired();
        b.Property(x => x.PhoneNumber).HasMaxLength(20).IsRequired();
        b.Property(x => x.CodeHash).IsRequired();
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => new { x.LandlordId, x.PhoneNumber, x.CreatedAt });
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.LandlordId).OnDelete(DeleteBehavior.NoAction);
    }
}
