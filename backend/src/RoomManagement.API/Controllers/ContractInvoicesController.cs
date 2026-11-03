using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomManagement.API.Contracts.Common;
using RoomManagement.API.Contracts.Invoices;
using RoomManagement.Business.Services;
using RoomManagement.Domain;

namespace RoomManagement.API.Controllers;

[ApiController]
[Authorize(Roles = UserRoles.Landlord)]
[Route("api/contracts/{contractId:guid}/invoices")]
public sealed class ContractInvoicesController : ControllerBase
{
    private readonly InvoiceService _service;
    public ContractInvoicesController(InvoiceService service) => _service = service;

    [HttpPost("deposit")]
    public async Task<IActionResult> CreateDeposit(Guid contractId, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created,
            new ApiResponse<InvoiceCreatedResponse>(InvoiceCreatedResponse.From(
                await _service.CreateDepositAsync(contractId, ct))));
}
