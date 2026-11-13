using RoomManagement.Domain.Enums;

namespace RoomManagement.Domain.Entities;

public sealed class Contract
{
    public Guid Id { get; set; }
    public string ContractCode { get; set; } = null!;
    public Guid RoomId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public decimal MonthlyRent { get; set; }
    public decimal DepositAmount { get; set; }
    public byte? PaymentDueDay { get; set; }
    public ContractStatus Status { get; set; } = ContractStatus.Draft;
    public ContractTenancyMode TenancyMode { get; set; } = ContractTenancyMode.CoTenancy;
    public string? Note { get; set; }
    public DateOnly? ClosedOn { get; set; }
    public DateOnly? HandoverOn { get; set; }
    public string? ClosureReason { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? ClosedByUserId { get; set; }

    public int OfferVersion { get; set; }
    public string? OfferTermsHash { get; set; }
    public string? OfferSnapshotJson { get; set; }
    public Guid? OfferRulesVersionId { get; set; }
    public DateTime? OfferSentAt { get; set; }
    public string? OfferSentByUserId { get; set; }

    public DateTime? TenantConfirmedAt { get; set; }
    public string? TenantConfirmedByUserId { get; set; }
    public int? TenantConfirmedOfferVersion { get; set; }
    public string? TenantConfirmedTermsHash { get; set; }
    public Guid? TenantConfirmedRulesVersionId { get; set; }

    public DateTime? TenantRejectedAt { get; set; }
    public string? TenantRejectedByUserId { get; set; }
    public string? TenantRejectionReason { get; set; }

    public DateTime? OperatorConfirmedAt { get; set; }
    public string? OperatorConfirmedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
