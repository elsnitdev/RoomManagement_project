using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Business.DTOs;

public sealed record MonthlyInvoiceInput(Guid ContractId, short Year, byte Month,
    decimal DiscountAmount = 0, string? DiscountReason = null, string? PreviewVersion = null);
public sealed record InvoiceFilter(int Page, int PageSize, Guid? RoomId,
    short? Year, byte? Month, InvoiceStatus? Status);
public sealed record CalculatedInvoiceItem(InvoiceItemType Type, string Description,
    decimal Quantity, decimal UnitPrice, decimal Amount, Guid? ReferenceId);
public sealed record BillableRoomService(Guid ServiceId, string Name, decimal UnitPrice, int Quantity);
public sealed record InvoicePreviewResult(Guid ContractId, Guid RoomId, short Year, byte Month,
    DateOnly IssueDate, DateOnly DueDate, IReadOnlyList<CalculatedInvoiceItem> Items,
    decimal TotalAmount)
{
    public MeterCostSnapshot? ElectricityReading { get; init; }
    public MeterCostSnapshot? WaterReading { get; init; }
    public string Version => InvoiceReviewVersion.Hash(new
    {
        ContractId, RoomId, Year, Month, IssueDate, DueDate, TotalAmount, Items,
        ElectricityReading, WaterReading
    });
}
public sealed record MeterCostSnapshot(decimal PreviousValue, decimal CurrentValue,
    decimal Usage, decimal UnitPrice, decimal Amount);
public sealed record MonthlyBillingCandidate(Guid ContractId, Guid RoomId, string RoomNumber,
    string ContractCode, Guid? InvoiceId, InvoiceStatus? InvoiceStatus);
public sealed record MonthlyBillingWorkItem(Guid ContractId, Guid RoomId, string RoomNumber,
    string ContractCode, string Status, IReadOnlyList<string> MissingInputs,
    Guid? InvoiceId, InvoiceStatus? InvoiceStatus);
public sealed record MonthlyBillingWorklist(short Year, byte Month,
    IReadOnlyList<MonthlyBillingWorkItem> Items);
public sealed record BillingBatchResult(Guid? ContractId, Guid? InvoiceId, string Status,
    string? Code = null, string? Message = null);
public sealed record InvoiceApproval(Guid InvoiceId, string Version);

public static class InvoiceReviewVersion
{
    public static string ForDraft(Invoice invoice, IEnumerable<InvoiceItem> items) => Hash(new
    {
        invoice.Id, invoice.Status, invoice.DueDate, invoice.TotalAmount,
        Items = items.OrderBy(x => x.Id).Select(x => new
        {
            x.Id, x.ItemType, x.Description, x.Quantity, x.UnitPrice, x.Amount, x.ReferenceId
        })
    });

    internal static string Hash<T>(T value) => Convert.ToHexString(
        System.Security.Cryptography.SHA256.HashData(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(value)));
}
public sealed record InvoiceBalanceResult(decimal EffectiveAmount, decimal TotalPaid,
    decimal TotalRefunded, decimal TotalDepositApplied, decimal RemainingAmount);
public sealed record InvoiceSummaryResult(Invoice Invoice, InvoiceBalanceResult Balance);
public sealed record InvoiceTenantResult(string FullName, string? PhoneNumber, string? Email,
    bool IsContactPerson, DateOnly JoinDate, DateOnly? LeaveDate);
public sealed record InvoiceDetailResult(Invoice Invoice,
    IReadOnlyList<InvoiceItem> Items, InvoiceBalanceResult Balance,
    IReadOnlyList<InvoiceTenantResult> Tenants)
{
    public MeterCostSnapshot? ElectricityReading { get; init; }
    public MeterCostSnapshot? WaterReading { get; init; }
    public IReadOnlyList<InvoiceAdjustment> Adjustments { get; init; } = [];
}
public sealed record EditableInvoiceItem(Guid? Id, InvoiceItemType Type, string Description,
    decimal Quantity, decimal UnitPrice);
public sealed record OneOffInvoiceInput(Guid ContractId, InvoiceType Type, DateOnly DueDate,
    string? Note, IReadOnlyList<EditableInvoiceItem> Items,
    string? Source = null, string? IdempotencyKey = null);
public sealed record DraftInvoiceItemsInput(IReadOnlyList<EditableInvoiceItem> Items);
public sealed record InvoiceAdjustmentInput(InvoiceAdjustmentType Type, decimal Amount,
    string Reason, string IdempotencyKey, bool LimitToRemainingAmount = false);
public sealed record PendingInvoicePayment(Guid InvoiceId, string InvoiceCode, Guid RoomId,
    Guid PaymentId, decimal ClaimedAmount, string? SubmittedByName, DateTime? PaidAt);
public sealed record InvoiceAttention(IReadOnlyList<InvoiceSummaryResult> Unpaid,
    IReadOnlyList<PendingInvoicePayment> PendingPayments);
