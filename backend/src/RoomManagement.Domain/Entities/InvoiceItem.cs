using RoomManagement.Domain.Enums;

namespace RoomManagement.Domain.Entities;

public sealed class InvoiceItem
{
    public Guid Id { get; set; }
    public Guid InvoiceId { get; set; }
    public InvoiceItemType ItemType { get; set; }
    public string Description { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
    public Guid? ReferenceId { get; set; }
    public DateTime CreatedAt { get; set; }
}
