using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Data.Configurations;

public sealed class RoomBatchSubmissionConfiguration : IEntityTypeConfiguration<RoomBatchSubmission>
{
    public void Configure(EntityTypeBuilder<RoomBatchSubmission> builder)
    {
        builder.ToTable("RoomBatchSubmissions", "dbo");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.OwnerUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.RequestHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ResultJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.PropertyId, x.OwnerUserId, x.IdempotencyKey }).IsUnique();
    }
}
