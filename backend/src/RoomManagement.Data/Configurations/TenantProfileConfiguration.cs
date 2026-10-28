using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Data.Configurations;

/// <summary>
/// Lớp cấu hình Fluent API của Entity Framework Core cho bảng TenantProfiles.
/// Định nghĩa cách ánh xạ từ class C# (TenantProfile) sang bảng SQL Server (dbo.TenantProfiles).
/// </summary>
public sealed class TenantProfileConfiguration : IEntityTypeConfiguration<TenantProfile>
{
    public void Configure(EntityTypeBuilder<TenantProfile> builder)
    {
        // 1. Ánh xạ vào bảng "TenantProfiles" thuộc schema "dbo" và thêm ràng buộc kiểm tra (Check Constraint) cho cột Gender
        builder.ToTable("TenantProfiles", "dbo", table =>
        {
            // Ràng buộc dưới SQL: cột Gender chỉ được phép NULL hoặc nằm trong 3 giá trị 'Male', 'Female', 'Other'
            table.HasCheckConstraint("CK_TenantProfiles_Gender", @"(Gender IS NULL OR Gender IN (N'Male', N'Female', N'Other'))");
        });

        // 2. Thiết lập khóa chính (Primary Key) là cột Id
        builder.HasKey(e => e.Id).HasName("PK_TenantProfiles");

        // 3. Cấu hình kiểu dữ liệu SQL và ràng buộc cho từng cột:
        // ValueGeneratedNever(): báo cho EF Core biết Id sẽ do code C# tự sinh (Guid.NewGuid()), SQL không tự sinh
        builder.Property(e => e.Id).HasColumnType("uniqueidentifier").IsRequired().ValueGeneratedNever();

        // UserId và CreatedByUserId có kiểu nvarchar(450) để khớp với kiểu khóa chính Id của bảng Users
        builder.Property(e => e.UserId).HasColumnType("nvarchar(450)");
        builder.Property(e => e.CreatedByUserId).HasColumnType("nvarchar(450)");

        // IsRequired(): tương đương với NOT NULL trong SQL Server
        builder.Property(e => e.FullName).HasColumnType("nvarchar(150)").IsRequired();
        builder.Property(e => e.PhoneNumber).HasColumnType("varchar(20)").IsRequired();

        // Các cột tùy chọn (không có IsRequired -> cho phép NULL trong SQL)
        builder.Property(e => e.Email).HasColumnType("varchar(150)");
        builder.Property(e => e.CitizenId).HasColumnType("varchar(20)");
        builder.Property(e => e.DateOfBirth).HasColumnType("date");
        builder.Property(e => e.Gender).HasColumnType("nvarchar(10)");
        builder.Property(e => e.PermanentAddress).HasColumnType("nvarchar(300)");

        // HasDefaultValueSql: nếu không truyền CreatedAt thì SQL tự lấy giờ UTC hiện tại bằng hàm SYSUTCDATETIME()
        builder.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").IsRequired().HasDefaultValueSql(@"SYSUTCDATETIME()", "DF_TenantProfiles_CreatedAt");
        builder.Property(e => e.UpdatedAt).HasColumnType("datetime2(0)");

        // 4. Thiết lập Khóa ngoại (Foreign Keys):
        // Liên kết tùy chọn từ TenantProfiles.UserId -> Users.Id (tài khoản đăng nhập của người thuê nếu có)
        builder.HasOne<AppUser>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_TenantProfiles_Users");

        // Liên kết từ TenantProfiles.CreatedByUserId -> Users.Id (tài khoản chủ trọ đã tạo hồ sơ này)
        builder.HasOne<AppUser>().WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_TenantProfiles_CreatedByUser");

        // 5. Thiết lập Chỉ mục (Indexes) để tăng tốc truy vấn và chống trùng lặp dữ liệu:
        // Filtered Unique Index: Chỉ kiểm tra trùng UserId khi UserId KHÁC NULL (cho phép nhiều hồ sơ cùng có UserId = NULL)
        builder.HasIndex(e => e.UserId).HasDatabaseName("UX_TenantProfiles_UserId").IsUnique().HasFilter(@"UserId IS NOT NULL");

        // Filtered Unique Index: Chống trùng số CCCD khi CitizenId KHÁC NULL (cho phép nhiều hồ sơ chưa nhập CCCD)
        builder.HasIndex(e => e.CitizenId).HasDatabaseName("UX_TenantProfiles_CitizenId").IsUnique().HasFilter(@"CitizenId IS NOT NULL");

        // Index thường trên cột CreatedByUserId để tăng tốc khi lọc danh sách hồ sơ theo từng chủ trọ
        builder.HasIndex(e => e.CreatedByUserId).HasDatabaseName("IX_TenantProfiles_CreatedByUserId");
    }
}
