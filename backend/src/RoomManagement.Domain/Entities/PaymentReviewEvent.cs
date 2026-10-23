using RoomManagement.Domain.Enums;

namespace RoomManagement.Domain.Entities;

public sealed class PaymentReviewEvent
{
    public Guid Id { get; set; }
    public Guid PaymentId { get; set; }
    public PaymentStatus PreviousStatus { get; set; }
    public PaymentStatus NewStatus { get; set; }
    public decimal? PreviousVerifiedAmount { get; set; }
    public decimal? NewVerifiedAmount { get; set; }
    public string Reason { get; set; } = null!;
    public string ReviewedByUserId { get; set; } = null!;
    public DateTime ReviewedAt { get; set; }
}
