using RoomManagement.Domain.Enums;

namespace RoomManagement.Business.DTOs;

public sealed record SettlementOverview(Guid ContractId, ContractStatus ContractStatus,
    short? PeriodYear, byte? PeriodMonth, string Phase, Guid? FinalInvoiceId,
    IReadOnlyList<InvoiceSummaryResult> Invoices, decimal OutstandingAmount, decimal DepositHeld,
    bool HasDraftInvoices, bool HasSettlement, IReadOnlyList<string> AllowedActions,
    IReadOnlyList<string> MissingMonthlyPeriods);
