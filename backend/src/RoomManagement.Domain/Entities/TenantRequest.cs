namespace RoomManagement.Domain.Entities;

public sealed class TenantRequest
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public Guid RequestedByTenantId { get; set; }
    public Guid? RelatedInvoiceId { get; set; }
    public string Category { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Priority { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string? AssignedToUserId { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNote { get; set; }
}
