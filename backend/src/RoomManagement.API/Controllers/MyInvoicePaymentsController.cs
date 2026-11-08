using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomManagement.API.Contracts.Common;
using RoomManagement.API.Contracts.Payments;
using RoomManagement.Business.Services;
using RoomManagement.Domain;

namespace RoomManagement.API.Controllers;

[ApiController]
[Authorize(Roles = UserRoles.Tenant)]
[Route("api/me/invoices/{invoiceId:guid}/payments")]
public sealed class MyInvoicePaymentsController : ControllerBase
{
    private readonly PaymentService _payments;

    public MyInvoicePaymentsController(PaymentService payments) => _payments = payments;

    [HttpPost]
    public async Task<IActionResult> Declare(Guid invoiceId,
        [FromBody] DeclareTenantPaymentRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst("sub")?.Value ?? "";
        var payment = await _payments.DeclareTenantAsync(invoiceId, userId, request.ToInput(), ct);
        return StatusCode(StatusCodes.Status201Created,
            new ApiResponse<PaymentResponse>(PaymentResponse.From(payment)));
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid invoiceId, CancellationToken ct)
    {
        var userId = User.FindFirst("sub")?.Value ?? "";
        var rows = await _payments.ListTenantAsync(invoiceId, userId, ct);
        return Ok(new ApiResponse<IReadOnlyList<PaymentResponse>>(
            rows.Select(PaymentResponse.From).ToList()));
    }
}
