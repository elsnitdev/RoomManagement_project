using RoomManagement.Domain.Enums;

namespace RoomManagement.Domain.Entities;

public sealed class Payment
{
    public Guid Id { get; set; }
    public Guid InvoiceId { get; set; }
    public decimal ClaimedAmount { get; set; }
    public decimal? VerifiedAmount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public PaymentStatus Status { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public string? VerifiedByUserId { get; set; }
    public string? TransactionCode { get; set; }
    public string IdempotencyKey { get; set; } = null!;
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}
