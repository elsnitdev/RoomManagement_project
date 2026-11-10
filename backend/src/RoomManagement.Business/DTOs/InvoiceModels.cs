using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Business.DTOs;

public sealed record MonthlyInvoiceInput(Guid ContractId, short Year, byte Month);
public sealed record InvoiceFilter(int Page, int PageSize, Guid? RoomId,
    short? Year, byte? Month, InvoiceStatus? Status);
public sealed record CalculatedInvoiceItem(InvoiceItemType Type, string Description,
    decimal Quantity, decimal UnitPrice, decimal Amount, Guid? ReferenceId);
public sealed record BillableRoomService(Guid ServiceId, string Name, decimal UnitPrice);
public sealed record InvoicePreviewResult(Guid ContractId, Guid RoomId, short Year, byte Month,
    DateOnly IssueDate, DateOnly DueDate, IReadOnlyList<CalculatedInvoiceItem> Items,
    decimal TotalAmount);
public sealed record InvoiceBalanceResult(decimal EffectiveAmount, decimal TotalPaid,
    decimal TotalRefunded, decimal TotalDepositApplied, decimal RemainingAmount);
public sealed record InvoiceSummaryResult(Invoice Invoice, InvoiceBalanceResult Balance);
public sealed record InvoiceTenantResult(string FullName, string? PhoneNumber, string? Email,
    bool IsContactPerson, DateOnly JoinDate, DateOnly? LeaveDate);
public sealed record InvoiceDetailResult(Invoice Invoice,
    IReadOnlyList<InvoiceItem> Items, InvoiceBalanceResult Balance,
    IReadOnlyList<InvoiceTenantResult> Tenants);
