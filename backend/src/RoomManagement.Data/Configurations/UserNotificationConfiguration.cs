using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Data.Configurations;

public sealed class UserNotificationConfiguration : IEntityTypeConfiguration<UserNotification>
{
    public void Configure(EntityTypeBuilder<UserNotification> builder)
    {
        builder.ToTable("UserNotifications", "dbo");
        builder.HasKey(e => e.Id).HasName("PK_UserNotifications");
        builder.Property(e => e.Id).HasColumnType("uniqueidentifier").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.UserId).HasColumnType("nvarchar(450)").IsRequired();
        builder.Property(e => e.Type).HasColumnType("nvarchar(60)").IsRequired();
        builder.Property(e => e.Title).HasColumnType("nvarchar(200)").IsRequired();
        builder.Property(e => e.Message).HasColumnType("nvarchar(1000)").IsRequired();
        builder.Property(e => e.ContractId).HasColumnType("uniqueidentifier");
        builder.Property(e => e.PropertyId).HasColumnType("uniqueidentifier");
        builder.Property(e => e.ActorUserId).HasColumnType("nvarchar(450)");
        builder.Property(e => e.IsRead).HasColumnType("bit").IsRequired().HasDefaultValueSql(@"0", "DF_UserNotifications_IsRead");
        builder.Property(e => e.ReadAt).HasColumnType("datetime2(0)");
        builder.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").IsRequired().HasDefaultValueSql(@"SYSUTCDATETIME()", "DF_UserNotifications_CreatedAt");

        builder.HasOne<AppUser>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_UserNotifications_Users");
        builder.HasIndex(e => new { e.UserId, e.IsRead, e.CreatedAt }).HasDatabaseName("IX_UserNotifications_User_Read_CreatedAt");
    }
}
