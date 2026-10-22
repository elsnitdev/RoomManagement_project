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
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}
