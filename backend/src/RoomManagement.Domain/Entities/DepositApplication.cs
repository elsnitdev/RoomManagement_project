namespace RoomManagement.Domain.Entities;

public sealed class DepositApplication
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public Guid TargetInvoiceId { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = null!;
    public string IdempotencyKey { get; set; } = null!;
    public string AppliedByUserId { get; set; } = null!;
    public DateTime AppliedAt { get; set; }
}
