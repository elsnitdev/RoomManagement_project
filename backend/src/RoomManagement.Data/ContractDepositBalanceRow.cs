namespace RoomManagement.Data;

public sealed class ContractDepositBalanceRow
{
    public Guid ContractId { get; set; }
    public Guid DepositInvoiceId { get; set; }
    public decimal DepositCollected { get; set; }
    public decimal DepositRefunded { get; set; }
    public decimal DepositApplied { get; set; }
    public decimal DepositHeld { get; set; }
}
