using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Data.Configurations;

public sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("Users", "dbo", table =>
        {
            table.HasCheckConstraint("CK_Users_Role", @"(Role IN (N'Admin', N'Landlord', N'Tenant'))");
            // Chỉ cho phép 3 trạng thái duyệt hợp lệ
            table.HasCheckConstraint("CK_Users_ApprovalStatus", @"(ApprovalStatus IN (N'Pending', N'Approved', N'Rejected'))");
        });
        builder.HasKey(e => e.Id).HasName("PK_Users");
        builder.Property(e => e.Id).HasColumnType("nvarchar(450)").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.UserName).HasColumnType("nvarchar(256)").IsRequired();
        builder.Property(e => e.NormalizedUserName).HasColumnType("nvarchar(256)").IsRequired();
        builder.Property(e => e.Email).HasColumnType("nvarchar(256)");
        builder.Property(e => e.NormalizedEmail).HasColumnType("nvarchar(256)");
        builder.Property(e => e.PasswordHash).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(e => e.Role).HasColumnType("nvarchar(20)").IsRequired();
        builder.Property(e => e.IsActive).HasColumnType("bit").IsRequired().HasDefaultValueSql(@"1", "DF_Users_IsActive");
        builder.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").IsRequired().HasDefaultValueSql(@"SYSUTCDATETIME()", "DF_Users_CreatedAt");
        builder.HasIndex(e => e.NormalizedUserName).HasDatabaseName("UX_Users_NormalizedUserName").IsUnique();
        builder.HasIndex(e => e.NormalizedEmail).HasDatabaseName("UX_Users_NormalizedEmail").IsUnique().HasFilter(@"NormalizedEmail IS NOT NULL");

        // --- Thông tin cá nhân (cho phép null vì tài khoản Admin tạo hộ chưa có) ---
        builder.Property(e => e.FullName).HasColumnType("nvarchar(150)");
        builder.Property(e => e.PhoneNumber).HasColumnType("varchar(20)");
        builder.Property(e => e.CitizenId).HasColumnType("varchar(20)");
        builder.Property(e => e.DateOfBirth).HasColumnType("date");
        builder.Property(e => e.Address).HasColumnType("nvarchar(300)");
        // Một số CCCD chỉ gắn với một tài khoản (bỏ qua các dòng chưa nhập CCCD)
        builder.HasIndex(e => e.CitizenId).HasDatabaseName("UX_Users_CitizenId").IsUnique().HasFilter(@"CitizenId IS NOT NULL");

        // --- Thông tin xét duyệt ---
        // Mặc định 'Approved' để các tài khoản đã có sẵn trong DB không bị khóa sau khi cập nhật
        builder.Property(e => e.ApprovalStatus).HasColumnType("nvarchar(20)").IsRequired().HasDefaultValueSql(@"N'Approved'", "DF_Users_ApprovalStatus");
        builder.Property(e => e.RejectionReason).HasColumnType("nvarchar(500)");
        builder.Property(e => e.ReviewedAt).HasColumnType("datetime2(0)");
    }
}
