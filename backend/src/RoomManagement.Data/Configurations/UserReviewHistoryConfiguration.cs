using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Data.Configurations;

public sealed class UserReviewHistoryConfiguration : IEntityTypeConfiguration<UserReviewHistory>
{
    public void Configure(EntityTypeBuilder<UserReviewHistory> builder)
    {
        builder.ToTable("UserReviewHistories", "dbo");
        builder.Property(x => x.TargetUserId).HasMaxLength(450);
        builder.Property(x => x.ActorAdminUserId).HasMaxLength(450);
        builder.Property(x => x.Action).HasMaxLength(40);
        builder.Property(x => x.FromStatus).HasMaxLength(20);
        builder.Property(x => x.ToStatus).HasMaxLength(20);
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.Property(x => x.VerificationMethod).HasMaxLength(100);
        builder.HasIndex(x => new { x.TargetUserId, x.CreatedAt });
    }
}
