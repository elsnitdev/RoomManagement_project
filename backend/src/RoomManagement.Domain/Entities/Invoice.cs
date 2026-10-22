namespace RoomManagement.Domain.Entities;

public sealed class Invoice
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public Guid RoomId { get; set; }
    public string InvoiceCode { get; set; } = null!;
    public string InvoiceType { get; set; } = null!;
    public short? BillingYear { get; set; }
    public byte? BillingMonth { get; set; }
    public DateOnly IssueDate { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = null!;
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}
