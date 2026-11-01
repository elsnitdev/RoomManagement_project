using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Data.Configurations;

public sealed class RoomRowConfiguration : IEntityTypeConfiguration<RoomRow>
{
    public void Configure(EntityTypeBuilder<RoomRow> builder)
    {
        builder.ToTable("RoomRows", "dbo");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.PropertyId, x.Code }).IsUnique();
    }
}
