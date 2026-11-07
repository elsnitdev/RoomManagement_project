using RoomManagement.Domain.Enums;

namespace RoomManagement.Domain.Entities;

/// <summary>Bản cam kết cá nhân của một thành viên trong một kỳ hợp đồng phòng.</summary>
public sealed class MemberCommitment
{
    public Guid Id { get; set; }
    public Guid ContractTenantId { get; set; }
    public CommitmentType CommitmentType { get; set; } = CommitmentType.RoomRulesAgreement;
    public DateOnly? SignedOn { get; set; }
    public string? SignerName { get; set; }
    public Guid? SignerTenantProfileId { get; set; }
    public string? SignerRelation { get; set; }
    public string? TermsSnapshot { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public string? ConfirmedByUserId { get; set; }
    public CommitmentEvidenceStatus EvidenceStatus { get; set; } = CommitmentEvidenceStatus.PendingUpload;
    public DateTime? SupersededAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
