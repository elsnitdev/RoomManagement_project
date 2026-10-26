using RoomManagement.Domain.Enums;

namespace RoomManagement.Domain.Entities;

public sealed class InvoiceAdjustment
{
    public Guid Id { get; set; }
    public Guid InvoiceId { get; set; }
    public InvoiceAdjustmentType AdjustmentType { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = null!;
    public string IdempotencyKey { get; set; } = null!;
    public string CreatedByUserId { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? VoidedAt { get; set; }
    public string? VoidReason { get; set; }
    public string? VoidedByUserId { get; set; }
}
