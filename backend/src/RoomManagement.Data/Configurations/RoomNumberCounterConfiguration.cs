using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Data.Configurations;

public sealed class RoomNumberCounterConfiguration : IEntityTypeConfiguration<RoomNumberCounter>
{
    public void Configure(EntityTypeBuilder<RoomNumberCounter> builder)
    {
        builder.ToTable("RoomNumberCounters", "dbo");
        builder.HasKey(x => new { x.PropertyId, x.ScopeKey });
        builder.Property(x => x.ScopeKey).HasMaxLength(100).IsRequired();
        builder.HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Cascade);
    }
}
