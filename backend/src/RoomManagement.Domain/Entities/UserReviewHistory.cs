namespace RoomManagement.Domain.Entities;

public sealed class UserReviewHistory
{
    public long Id { get; set; }
    public string TargetUserId { get; set; } = null!;
    public string? ActorAdminUserId { get; set; }
    public string Action { get; set; } = null!;
    public string FromStatus { get; set; } = null!;
    public string ToStatus { get; set; } = null!;
    public string? Reason { get; set; }
    public string? VerificationMethod { get; set; }
    public DateTime CreatedAt { get; set; }
}
