namespace RoomManagement.Domain.Entities;

public sealed class PaymentReviewEvent
{
    public Guid Id { get; set; }
    public Guid PaymentId { get; set; }
    public string PreviousStatus { get; set; } = null!;
    public string NewStatus { get; set; } = null!;
    public decimal? PreviousVerifiedAmount { get; set; }
    public decimal? NewVerifiedAmount { get; set; }
    public string Reason { get; set; } = null!;
    public string ReviewedByUserId { get; set; } = null!;
    public DateTime ReviewedAt { get; set; }
}
