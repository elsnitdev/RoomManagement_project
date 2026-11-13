using RoomManagement.Business.DTOs;
using RoomManagement.Business.Interfaces;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Business.Services;

public sealed class SettlementOverviewService(ContractService contracts, IInvoiceStore invoices)
{
    public static string DeterminePhase(ContractStatus status, bool finalIssued,
        bool hasOpenInvoices, bool hasMissingMonthlyPeriods) => status switch
    {
        ContractStatus.PendingMoveOut => "Handover",
        ContractStatus.PendingSettlement when !finalIssued => "PrepareInvoice",
        ContractStatus.PendingSettlement when hasOpenInvoices || hasMissingMonthlyPeriods => "Collect",
        ContractStatus.PendingSettlement => "Complete",
        _ => "Closed"
    };

    public async Task<SettlementOverview> GetAsync(Guid propertyId, Guid contractId, CancellationToken ct)
    {
        var detail = await contracts.GetDetailAsync(propertyId, contractId, ct);
        var contract = detail.Contract;
        var rows = await invoices.ListForContractAsync(contractId, ct);
        var period = contract.HandoverOn;
        var final = period is null ? null : rows.FirstOrDefault(x =>
            x.Invoice.InvoiceType == InvoiceType.Monthly &&
            x.Invoice.BillingYear == period.Value.Year && x.Invoice.BillingMonth == period.Value.Month);
        var missingPeriods = new List<string>();
        if (period is not null)
        {
            var issued = rows.Where(x => x.Invoice.InvoiceType == InvoiceType.Monthly &&
                x.Invoice.Status == InvoiceStatus.Issued && x.Invoice.BillingYear is not null &&
                x.Invoice.BillingMonth is not null)
                .Select(x => (x.Invoice.BillingYear!.Value, x.Invoice.BillingMonth!.Value)).ToHashSet();
            for (var month = new DateOnly(contract.StartDate.Year, contract.StartDate.Month, 1);
                month <= period.Value; month = month.AddMonths(1))
                if (!issued.Contains(((short)month.Year, (byte)month.Month)))
                    missingPeriods.Add($"{month.Month:D2}/{month.Year}");
        }
        var pending = rows.Any(x => x.Invoice.Status == InvoiceStatus.Draft ||
            x.Invoice.Status == InvoiceStatus.Issued && x.Balance.RemainingAmount > 0);
        var phase = DeterminePhase(contract.Status, final?.Invoice.Status == InvoiceStatus.Issued,
            pending, missingPeriods.Count > 0);
        var actions = new List<string>();
        if (phase == "Handover") actions.AddRange(["RecordReading", "Handover"]);
        if (phase == "PrepareInvoice")
        {
            if (final is null) actions.Add("CreateMonthlyInvoice");
            actions.Add("CreateSettlementOneOff");
        }
        if (contract.Status == ContractStatus.PendingSettlement)
        {
            if (rows.Any(x => x.Invoice.Status == InvoiceStatus.Draft)) actions.Add("IssueInvoice");
            if (rows.Any(x => x.Invoice.Status == InvoiceStatus.Issued && x.Balance.RemainingAmount > 0))
                actions.Add("RecordPayment");
            if (final?.Invoice.Status == InvoiceStatus.Issued && final.Balance.RemainingAmount > 0 && detail.DepositHeld > 0)
                actions.Add("ApplyDeposit");
            if (phase == "Complete") actions.Add("CompleteSettlement");
        }
        return new SettlementOverview(contractId, contract.Status,
            period is null ? null : (short)period.Value.Year,
            period is null ? null : (byte)period.Value.Month, phase, final?.Invoice.Id,
            rows, detail.OutstandingAmount, detail.DepositHeld, detail.HasDraftInvoices,
            detail.HasSettlement, actions, missingPeriods);
    }
}
