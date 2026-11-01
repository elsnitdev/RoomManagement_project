namespace RoomManagement.Domain.Entities;

public sealed class RoomBatchSubmission
{
    public Guid Id { get; set; }
    public Guid PropertyId { get; set; }
    public string OwnerUserId { get; set; } = null!;
    public Guid IdempotencyKey { get; set; }
    public string RequestHash { get; set; } = null!;
    public string ResultJson { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}
