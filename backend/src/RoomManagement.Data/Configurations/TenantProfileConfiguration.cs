using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Data.Configurations;

public sealed class TenantProfileConfiguration : IEntityTypeConfiguration<TenantProfile>
{
    public void Configure(EntityTypeBuilder<TenantProfile> builder)
    {
        builder.ToTable("TenantProfiles", "dbo", table =>
        {
            table.HasCheckConstraint("CK_TenantProfiles_Gender", @"(Gender IS NULL OR Gender IN (N'Male', N'Female', N'Other'))");
        });
        builder.HasKey(e => e.Id).HasName("PK_TenantProfiles");
        builder.Property(e => e.Id).HasColumnType("uniqueidentifier").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.UserId).HasColumnType("nvarchar(450)");
        builder.Property(e => e.CreatedByUserId).HasColumnType("nvarchar(450)");
        builder.Property(e => e.FullName).HasColumnType("nvarchar(150)").IsRequired();
        builder.Property(e => e.PhoneNumber).HasColumnType("varchar(20)").IsRequired();
        builder.Property(e => e.Email).HasColumnType("varchar(150)");
        builder.Property(e => e.CitizenId).HasColumnType("varchar(20)");
        builder.Property(e => e.DateOfBirth).HasColumnType("date");
        builder.Property(e => e.Gender).HasColumnType("nvarchar(10)");
        builder.Property(e => e.PermanentAddress).HasColumnType("nvarchar(300)");
        builder.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").IsRequired().HasDefaultValueSql(@"SYSUTCDATETIME()", "DF_TenantProfiles_CreatedAt");
        builder.Property(e => e.UpdatedAt).HasColumnType("datetime2(0)");
        builder.HasOne<AppUser>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_TenantProfiles_Users");
        builder.HasOne<AppUser>().WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_TenantProfiles_CreatedByUser");
        builder.HasIndex(e => e.UserId).HasDatabaseName("UX_TenantProfiles_UserId").IsUnique().HasFilter(@"UserId IS NOT NULL");
        builder.HasIndex(e => e.CitizenId).HasDatabaseName("UX_TenantProfiles_CitizenId").IsUnique().HasFilter(@"CitizenId IS NOT NULL");
        builder.HasIndex(e => e.CreatedByUserId).HasDatabaseName("IX_TenantProfiles_CreatedByUserId");
    }
}
