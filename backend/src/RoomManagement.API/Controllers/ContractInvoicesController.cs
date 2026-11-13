using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomManagement.API.Contracts.Common;
using RoomManagement.API.Contracts.Invoices;
using RoomManagement.Business.Services;
using RoomManagement.Domain;

namespace RoomManagement.API.Controllers;

[ApiController]
[Authorize(Roles = UserRoles.LandlordOrManager)]
[Route("api/contracts/{contractId:guid}/invoices")]
public sealed class ContractInvoicesController : ControllerBase
{
    private readonly InvoiceService _service;
    public ContractInvoicesController(InvoiceService service) => _service = service;

    [HttpGet("deposit")]
    public async Task<IActionResult> GetDeposit(Guid contractId, CancellationToken ct)
    {
        var invoice = await _service.FindDepositAsync(contractId, ct);
        return Ok(new ApiResponse<InvoiceCreatedResponse?>(
            invoice is null ? null : InvoiceCreatedResponse.From(invoice)));
    }

    [HttpPost("deposit")]
    public async Task<IActionResult> CreateDeposit(Guid contractId, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created,
            new ApiResponse<InvoiceCreatedResponse>(InvoiceCreatedResponse.From(
                await _service.CreateDepositAsync(contractId, ct))));
}
