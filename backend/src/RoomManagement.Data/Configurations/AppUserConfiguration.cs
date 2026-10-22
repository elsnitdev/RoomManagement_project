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
    }
}
