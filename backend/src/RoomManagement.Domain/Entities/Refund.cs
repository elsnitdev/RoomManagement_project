namespace RoomManagement.Domain.Entities;

public sealed class Refund
{
    public Guid Id { get; set; }
    public Guid InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = null!;
    public DateTime? RefundedAt { get; set; }
    public string Reason { get; set; } = null!;
    public string? TransactionCode { get; set; }
    public string IdempotencyKey { get; set; } = null!;
    public string CreatedByUserId { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}
