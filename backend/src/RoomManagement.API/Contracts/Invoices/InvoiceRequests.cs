using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using RoomManagement.Business.DTOs;
using RoomManagement.Domain.Enums;

namespace RoomManagement.API.Contracts.Invoices;

public sealed record MonthlyInvoiceRequest(
    [property: JsonRequired] Guid ContractId,
    [property: JsonRequired][Range(2000, 2100)] short Year,
    [property: JsonRequired][Range(1, 12)] byte Month,
    decimal DiscountAmount = 0,
    [StringLength(255)] string? DiscountReason = null)
{
    public MonthlyInvoiceInput ToInput() => new(ContractId, Year, Month, DiscountAmount, DiscountReason);
}

public sealed record EditableInvoiceItemRequest(Guid? Id, InvoiceItemType Type,
    string Description, decimal Quantity, decimal UnitPrice)
{
    public EditableInvoiceItem ToInput() => new(Id, Type, Description, Quantity, UnitPrice);
}

public sealed record OneOffInvoiceRequest(Guid ContractId, InvoiceType Type, DateOnly DueDate,
    string? Note, IReadOnlyList<EditableInvoiceItemRequest> Items,
    string? Source = null, string? IdempotencyKey = null)
{
    public OneOffInvoiceInput ToInput() => new(ContractId, Type, DueDate, Note,
        Items?.Select(x => x.ToInput()).ToList() ?? [], Source, IdempotencyKey);
}

public sealed record DraftInvoiceItemsRequest(IReadOnlyList<EditableInvoiceItemRequest> Items)
{
    public DraftInvoiceItemsInput ToInput() => new(Items?.Select(x => x.ToInput()).ToList() ?? []);
}

public sealed record InvoiceAdjustmentRequest(InvoiceAdjustmentType Type, decimal Amount,
    string Reason, string IdempotencyKey)
{
    public InvoiceAdjustmentInput ToInput() => new(Type, Amount, Reason, IdempotencyKey);
}
