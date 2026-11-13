using RoomManagement.Business.DTOs;
using RoomManagement.Business.Errors;
using RoomManagement.Business.Services;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;
using Xunit;

namespace RoomManagement.Business.Tests;

public sealed class SettlementWorkflowTests
{
    [Fact]
    public void Paid_final_invoice_with_unpaid_one_off_resumes_collection()
    {
        Assert.Equal("Collect", SettlementOverviewService.DeterminePhase(
            ContractStatus.PendingSettlement, finalIssued: true,
            hasOpenInvoices: true, hasMissingMonthlyPeriods: false));
    }

    [Fact]
    public void Fully_paid_contract_can_complete_once()
    {
        Assert.Equal("Complete", SettlementOverviewService.DeterminePhase(
            ContractStatus.PendingSettlement, finalIssued: true,
            hasOpenInvoices: false, hasMissingMonthlyPeriods: false));
        Assert.Equal("Closed", SettlementOverviewService.DeterminePhase(
            ContractStatus.Terminated, finalIssued: true,
            hasOpenInvoices: false, hasMissingMonthlyPeriods: false));
    }

    [Fact]
    public void Missing_older_month_keeps_collection_open()
    {
        Assert.Equal("Collect", SettlementOverviewService.DeterminePhase(
            ContractStatus.PendingSettlement, finalIssued: true,
            hasOpenInvoices: false, hasMissingMonthlyPeriods: true));
    }

    [Fact]
    public void Charge_fingerprint_detects_same_fee_despite_due_date_change()
    {
        var contractId = Guid.NewGuid();
        EditableInvoiceItem[] items = [new(null, InvoiceItemType.Damage, "  Sửa khóa  ", 1, 250_000m)];
        EditableInvoiceItem[] same = [new(null, InvoiceItemType.Damage, "Sửa khóa", 1, 250_000m)];
        Assert.Equal(
            SettlementChargeIdentity.ChargeFingerprint(contractId, InvoiceType.Damage, items),
            SettlementChargeIdentity.ChargeFingerprint(contractId, InvoiceType.Damage, same));
        Assert.NotEqual(
            SettlementChargeIdentity.RequestHash(contractId, InvoiceType.Damage,
                new DateOnly(2026, 11, 15), null, items),
            SettlementChargeIdentity.RequestHash(contractId, InvoiceType.Damage,
                new DateOnly(2026, 11, 16), null, same));
    }

    [Fact]
    public void Distinct_fee_purposes_remain_distinct()
    {
        var contractId = Guid.NewGuid();
        EditableInvoiceItem[] repair = [new(null, InvoiceItemType.Damage, "Sửa khóa", 1, 250_000m)];
        EditableInvoiceItem[] cleaning = [new(null, InvoiceItemType.Other, "Vệ sinh", 1, 250_000m)];
        Assert.NotEqual(
            SettlementChargeIdentity.ChargeFingerprint(contractId, InvoiceType.Damage, repair),
            SettlementChargeIdentity.ChargeFingerprint(contractId, InvoiceType.Other, cleaning));
    }

    [Fact]
    public void Same_operation_key_and_payload_returns_existing_invoice()
    {
        var invoice = new Invoice { Id = Guid.NewGuid(), SettlementRequestHash = "ABC" };
        Assert.Same(invoice, SettlementChargeIdentity.VerifyRetry(invoice, "ABC"));
    }

    [Fact]
    public void Reusing_operation_key_for_changed_payload_is_conflict()
    {
        var invoice = new Invoice { Id = Guid.NewGuid(), SettlementRequestHash = "ABC" };
        var error = Assert.Throws<BusinessException>(() => SettlementChargeIdentity.VerifyRetry(invoice, "DEF"));
        Assert.Equal("SETTLEMENT_OPERATION_KEY_CONFLICT", error.Code);
    }
}
