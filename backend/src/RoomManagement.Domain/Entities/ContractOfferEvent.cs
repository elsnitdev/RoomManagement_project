namespace RoomManagement.Domain.Entities;

/// <summary>
/// Nhật ký bất biến các mốc gửi đề nghị, xác nhận của người thuê đại diện, từ chối và đối ký của phía chủ trọ.
/// </summary>
public sealed class ContractOfferEvent
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public int OfferVersion { get; set; }
    public string Action { get; set; } = null!; // Sent, TenantConfirmed, TenantRejected, OperatorCountersigned, Reset, Cancelled
    public string TermsHash { get; set; } = null!;
    public Guid RulesVersionId { get; set; }
    public string SnapshotJson { get; set; } = null!;
    public string ActorUserId { get; set; } = null!;
    public string? ActorIdentityVerificationStatus { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}
