using RoomManagement.Domain.Enums;

namespace RoomManagement.Domain.Entities;

public sealed class TenantRequest
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public Guid RequestedByTenantId { get; set; }
    public Guid? RelatedInvoiceId { get; set; }
    public TenantRequestCategory Category { get; set; }
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public TenantRequestPriority Priority { get; set; } = TenantRequestPriority.Medium;
    public TenantRequestStatus Status { get; set; } = TenantRequestStatus.Pending;
    public string? AssignedToUserId { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNote { get; set; }
}
