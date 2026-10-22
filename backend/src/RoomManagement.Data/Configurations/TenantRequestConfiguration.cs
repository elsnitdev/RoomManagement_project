using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Data.Configurations;

public sealed class TenantRequestConfiguration : IEntityTypeConfiguration<TenantRequest>
{
    public void Configure(EntityTypeBuilder<TenantRequest> builder)
    {
        builder.ToTable("TenantRequests", "dbo", table =>
        {
            table.HasCheckConstraint("CK_TenantRequests_Category", @"(Category IN (N'Repair', N'Billing', N'Question', N'Complaint', N'Other'))");
            table.HasCheckConstraint("CK_TenantRequests_Priority", @"(Priority IN (N'Low', N'Medium', N'High'))");
            table.HasCheckConstraint("CK_TenantRequests_Status", @"(Status IN (N'Pending', N'InProgress', N'Resolved'))");
            table.HasCheckConstraint("CK_TenantRequests_ResolvedAt", @"((Status = N'Resolved' AND ResolvedAt IS NOT NULL) OR (Status <> N'Resolved' AND ResolvedAt IS NULL))");
        });
        builder.HasKey(e => e.Id).HasName("PK_TenantRequests");
        builder.Property(e => e.Id).HasColumnType("uniqueidentifier").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.ContractId).HasColumnType("uniqueidentifier").IsRequired();
        builder.Property(e => e.RequestedByTenantId).HasColumnType("uniqueidentifier").IsRequired();
        builder.Property(e => e.RelatedInvoiceId).HasColumnType("uniqueidentifier");
        builder.Property(e => e.Category).HasColumnType("nvarchar(20)").IsRequired();
        builder.Property(e => e.Title).HasColumnType("nvarchar(200)").IsRequired();
        builder.Property(e => e.Description).HasColumnType("nvarchar(2000)").IsRequired();
        builder.Property(e => e.Priority).HasColumnType("nvarchar(10)").IsRequired().HasDefaultValueSql(@"N'Medium'", "DF_TenantRequests_Priority");
        builder.Property(e => e.Status).HasColumnType("nvarchar(20)").IsRequired().HasDefaultValueSql(@"N'Pending'", "DF_TenantRequests_Status");
        builder.Property(e => e.AssignedToUserId).HasColumnType("nvarchar(450)");
        builder.Property(e => e.RequestedAt).HasColumnType("datetime2(0)").IsRequired().HasDefaultValueSql(@"SYSUTCDATETIME()", "DF_TenantRequests_RequestedAt");
        builder.Property(e => e.UpdatedAt).HasColumnType("datetime2(0)");
        builder.Property(e => e.ResolvedAt).HasColumnType("datetime2(0)");
        builder.Property(e => e.ResolutionNote).HasColumnType("nvarchar(1000)");
        builder.HasOne<Contract>().WithMany().HasForeignKey(e => e.ContractId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_TenantRequests_Contracts");
        builder.HasOne<TenantProfile>().WithMany().HasForeignKey(e => e.RequestedByTenantId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_TenantRequests_Tenants");
        builder.HasOne<Invoice>().WithMany().HasForeignKey(e => new { e.RelatedInvoiceId, e.ContractId }).HasPrincipalKey(e => new { e.Id, e.ContractId }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_TenantRequests_Invoices");
        builder.HasOne<AppUser>().WithMany().HasForeignKey(e => e.AssignedToUserId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_TenantRequests_AssignedTo");
        builder.HasIndex(e => new { e.ContractId, e.Status }).HasDatabaseName("IX_TenantRequests_Contract_Status");
        builder.HasIndex(e => new { e.RequestedByTenantId, e.Status }).HasDatabaseName("IX_TenantRequests_Tenant_Status");
        builder.HasIndex(e => new { e.AssignedToUserId, e.Status }).HasDatabaseName("IX_TenantRequests_Assigned_Status").HasFilter(@"AssignedToUserId IS NOT NULL");
    }
}
