using RoomManagement.Domain.Enums;

namespace RoomManagement.Domain.Entities;

public sealed class Invoice
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public Guid RoomId { get; set; }
    public string InvoiceCode { get; set; } = null!;
    public InvoiceType InvoiceType { get; set; }
    public short? BillingYear { get; set; }
    public byte? BillingMonth { get; set; }
    public DateOnly IssueDate { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal TotalAmount { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}
