using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Data.Configurations;

public sealed class RoomImageConfiguration : IEntityTypeConfiguration<RoomImage>
{
    public void Configure(EntityTypeBuilder<RoomImage> b)
    {
        b.ToTable("RoomImages", "dbo", t =>
        {
            t.HasCheckConstraint("CK_RoomImages_Size", "[SizeBytes] > 0");
            t.HasCheckConstraint("CK_RoomImages_Order", "[SortOrder] >= 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.BlobName).HasMaxLength(500).IsRequired();
        b.Property(x => x.OriginalFileName).HasMaxLength(255).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        b.Property(x => x.CreatedAt).HasColumnType("datetime2(0)");
        b.HasOne<Room>().WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(x => x.BlobName).IsUnique();
        b.HasIndex(x => new { x.RoomId, x.SortOrder, x.Id });
    }
}
