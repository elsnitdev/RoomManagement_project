using RoomManagement.Business.DTOs;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.API.Contracts.Invoices;

public sealed record InvoiceItemResponse(Guid Id, InvoiceItemType Type, string Description,
    decimal Quantity, decimal UnitPrice, decimal Amount, Guid? ReferenceId)
{
    public static InvoiceItemResponse From(InvoiceItem x) =>
        new(x.Id, x.ItemType, x.Description, x.Quantity, x.UnitPrice, x.Amount, x.ReferenceId);
    public static InvoiceItemResponse From(CalculatedInvoiceItem x) =>
        new(Guid.Empty, x.Type, x.Description, x.Quantity, x.UnitPrice, x.Amount, x.ReferenceId);
}

public sealed record InvoicePreviewResponse(Guid ContractId, Guid RoomId, short Year, byte Month,
    DateOnly IssueDate, DateOnly DueDate, decimal TotalAmount,
    IReadOnlyList<InvoiceItemResponse> Items)
{
    public MeterCostSnapshot? ElectricityReading { get; init; }
    public MeterCostSnapshot? WaterReading { get; init; }
    public static InvoicePreviewResponse From(InvoicePreviewResult x) =>
        new(x.ContractId, x.RoomId, x.Year, x.Month, x.IssueDate, x.DueDate,
            x.TotalAmount, x.Items.Select(InvoiceItemResponse.From).ToList())
        { ElectricityReading = x.ElectricityReading, WaterReading = x.WaterReading };
}

public sealed record InvoiceSummaryResponse(Guid Id, string Code, Guid ContractId, Guid RoomId,
    InvoiceType Type, InvoiceStatus Status, string DisplayStatus,
    short? BillingYear, byte? BillingMonth, DateOnly IssueDate, DateOnly DueDate,
    decimal BaseTotal, decimal EffectiveTotal, decimal TotalPaid, decimal TotalRefunded,
    decimal TotalDepositApplied, decimal RemainingAmount, decimal AmountDue, decimal CreditToRefund)
{
    public static InvoiceSummaryResponse From(InvoiceSummaryResult x) => From(x.Invoice, x.Balance);

    public static InvoiceSummaryResponse From(Invoice invoice, InvoiceBalanceResult balance)
    {
        var remaining = balance.RemainingAmount;
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var display = invoice.Status != InvoiceStatus.Issued ? invoice.Status.ToString() :
            remaining < 0 ? "Credit" : remaining == 0 ? "Paid" :
            invoice.DueDate < today ? "Overdue" :
            balance.TotalPaid + balance.TotalDepositApplied > 0 ? "Partial" : "Unpaid";
        return new(invoice.Id, invoice.InvoiceCode, invoice.ContractId, invoice.RoomId,
            invoice.InvoiceType, invoice.Status, display, invoice.BillingYear, invoice.BillingMonth,
            invoice.IssueDate, invoice.DueDate, invoice.TotalAmount, balance.EffectiveAmount,
            balance.TotalPaid, balance.TotalRefunded, balance.TotalDepositApplied, remaining,
            Math.Max(0, remaining), Math.Max(0, -remaining));
    }
}

public sealed record InvoiceDetailResponse(InvoiceSummaryResponse Summary,
    IReadOnlyList<InvoiceItemResponse> Items, IReadOnlyList<InvoiceTenantResult> Tenants)
{
    public MeterCostSnapshot? ElectricityReading { get; init; }
    public MeterCostSnapshot? WaterReading { get; init; }
    public IReadOnlyList<InvoiceAdjustmentResponse> Adjustments { get; init; } = [];
    public static InvoiceDetailResponse From(InvoiceDetailResult x) =>
        new(InvoiceSummaryResponse.From(x.Invoice, x.Balance),
            x.Items.Select(InvoiceItemResponse.From).ToList(), x.Tenants)
        {
            ElectricityReading = x.ElectricityReading,
            WaterReading = x.WaterReading,
            Adjustments = x.Adjustments.Select(InvoiceAdjustmentResponse.From).ToList()
        };
}

public sealed record InvoiceAdjustmentResponse(Guid Id, InvoiceAdjustmentType Type,
    decimal Amount, string Reason, DateTime CreatedAt, DateTime? VoidedAt)
{
    public static InvoiceAdjustmentResponse From(InvoiceAdjustment x) =>
        new(x.Id, x.AdjustmentType, x.Amount, x.Reason, x.CreatedAt, x.VoidedAt);
}

public sealed record InvoiceCreatedResponse(Guid Id, string Code, InvoiceStatus Status)
{
    public static InvoiceCreatedResponse From(Invoice x) => new(x.Id, x.InvoiceCode, x.Status);
}
