namespace RoomManagement.Data;

public sealed class InvoiceBalanceRow
{
    public Guid InvoiceId { get; set; }
    public Guid ContractId { get; set; }
    public Guid RoomId { get; set; }
    public string InvoiceType { get; set; } = null!;
    public string Status { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public decimal TotalAdjustments { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalRefunded { get; set; }
    public decimal TotalDepositApplied { get; set; }
    public decimal EffectiveAmount { get; set; }
    public decimal RemainingAmount { get; set; }
}
