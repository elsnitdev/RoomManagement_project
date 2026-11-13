using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomManagement.API.Contracts.Common;
using RoomManagement.API.Contracts.Invoices;
using RoomManagement.Business.DTOs;
using RoomManagement.Business.Services;
using RoomManagement.Domain;
using RoomManagement.Domain.Enums;

namespace RoomManagement.API.Controllers;

[ApiController]
[Authorize(Roles = UserRoles.LandlordOrManager)]
[Route("api/properties/{propertyId:guid}/contracts/{contractId:guid}/settlement-overview")]
public sealed class SettlementOverviewController(SettlementOverviewService service) : ControllerBase
{
    public sealed record SettlementOverviewResponse(Guid ContractId, ContractStatus ContractStatus,
        short? PeriodYear, byte? PeriodMonth, string Phase, Guid? FinalInvoiceId,
        IReadOnlyList<InvoiceSummaryResponse> Invoices, decimal OutstandingAmount, decimal DepositHeld,
        bool HasDraftInvoices, bool HasSettlement, IReadOnlyList<string> AllowedActions,
        IReadOnlyList<string> MissingMonthlyPeriods)
    {
        public static SettlementOverviewResponse From(SettlementOverview value) => new(
            value.ContractId, value.ContractStatus, value.PeriodYear, value.PeriodMonth,
            value.Phase, value.FinalInvoiceId, value.Invoices.Select(InvoiceSummaryResponse.From).ToList(),
            value.OutstandingAmount, value.DepositHeld, value.HasDraftInvoices, value.HasSettlement,
            value.AllowedActions, value.MissingMonthlyPeriods);
    }

    [HttpGet]
    public async Task<IActionResult> Get(Guid propertyId, Guid contractId, CancellationToken ct) =>
        Ok(new ApiResponse<SettlementOverviewResponse>(SettlementOverviewResponse.From(
            await service.GetAsync(propertyId, contractId, ct))));
}
