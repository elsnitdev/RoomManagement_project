using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomManagement.API.Contracts.Common;
using RoomManagement.API.Contracts.Invoices;
using RoomManagement.Business.DTOs;
using RoomManagement.Business.Services;
using RoomManagement.Domain;

namespace RoomManagement.API.Controllers;

[ApiController]
[Authorize(Roles = UserRoles.Tenant)]
[Route("api/me/invoices")]
public sealed class MyInvoicesController : ControllerBase
{
    private readonly InvoiceService _service;
    public MyInvoicesController(InvoiceService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] short? year = null, [FromQuery] byte? month = null,
        CancellationToken ct = default)
    {
        var userId = User.FindFirst("sub")?.Value ?? "";
        var result = await _service.ListMineAsync(userId,
            new InvoiceFilter(page, pageSize, null, year, month, null), ct);
        return Ok(new PageResponse<InvoiceSummaryResponse>(
            result.Items.Select(InvoiceSummaryResponse.From).ToList(),
            new PageMeta(page, pageSize, result.TotalItems,
                (int)Math.Ceiling((double)result.TotalItems / pageSize))));
    }

    [HttpGet("{invoiceId:guid}")]
    public async Task<IActionResult> Get(Guid invoiceId, CancellationToken ct)
    {
        var userId = User.FindFirst("sub")?.Value ?? "";
        return Ok(new ApiResponse<InvoiceDetailResponse>(InvoiceDetailResponse.From(
            await _service.GetMineAsync(userId, invoiceId, ct))));
    }

    [HttpGet("{invoiceId:guid}/payment-account")]
    public async Task<IActionResult> PaymentAccount(Guid invoiceId, CancellationToken ct)
    {
        var userId = User.FindFirst("sub")?.Value ?? "";
        var property = await _service.GetMyPaymentPropertyAsync(userId, invoiceId, ct);
        return Ok(new ApiResponse<object>(new
        {
            propertyName = property.Name,
            bankName = property.BankName,
            accountNumber = property.BankAccountNumber,
            accountHolder = property.BankAccountHolder,
            canDeclarePayment = await _service.CanDeclareMyPaymentAsync(userId, invoiceId, ct)
        }));
    }
}
