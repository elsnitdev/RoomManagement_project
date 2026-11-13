namespace RoomManagement.Domain.Entities;

public sealed class ContractSettlement
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public DateOnly SettlementDate { get; set; }
    public decimal DepositAmount { get; set; }
    public decimal OutstandingAmount { get; set; }
    public decimal DamageFee { get; set; }
    public decimal OtherFee { get; set; }
    public decimal RefundAmount { get; set; }
    public string DepositDisposition { get; set; } = "NoDeposit";
    public string? Note { get; set; }
    public Guid? RefundEvidenceId { get; set; }
    public string? RefundEvidenceBlobName { get; set; }
    public string? RefundEvidenceFileName { get; set; }
    public string? RefundEvidenceContentType { get; set; }
    public long? RefundEvidenceSizeBytes { get; set; }
    public string? RefundEvidenceSha256 { get; set; }
    public DateTime? RefundEvidenceUploadedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
