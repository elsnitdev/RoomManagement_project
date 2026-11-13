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
[Authorize(Roles = UserRoles.Landlord)]
[Route("api/properties/{propertyId:guid}/invoices")]
public sealed class PropertyInvoicesController : ControllerBase
{
    private readonly InvoiceService _service;
    public PropertyInvoicesController(InvoiceService service) => _service = service;

    [HttpGet("monthly/worklist")]
    public async Task<IActionResult> MonthlyWorklist(Guid propertyId, [FromQuery] short? year,
        [FromQuery] byte? month, CancellationToken ct) =>
        Ok(new ApiResponse<MonthlyBillingWorklist>(
            await _service.MonthlyWorklistAsync(propertyId, year, month, ct)));

    [HttpGet("attention")]
    public async Task<IActionResult> Attention(Guid propertyId, CancellationToken ct)
    {
        var result = await _service.AttentionAsync(propertyId, ct);
        return Ok(new ApiResponse<object>(new
        {
            Unpaid = result.Unpaid.Select(InvoiceSummaryResponse.From).ToList(),
            result.PendingPayments
        }));
    }

    [HttpPost("one-off")]
    public async Task<IActionResult> CreateOneOff(Guid propertyId,
        [FromBody] OneOffInvoiceRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, new ApiResponse<InvoiceCreatedResponse>(
            InvoiceCreatedResponse.From(await _service.CreateOneOffAsync(propertyId, request.ToInput(), ct))));

    [HttpPost("monthly/preview")]
    public async Task<IActionResult> Preview(Guid propertyId, [FromBody] MonthlyInvoiceRequest request, CancellationToken ct) =>
        Ok(new ApiResponse<InvoicePreviewResponse>(InvoicePreviewResponse.From(
            await _service.PreviewMonthlyAsync(propertyId, request.ToInput(), ct))));

    [HttpPost("monthly")]
    public async Task<IActionResult> CreateMonthly(Guid propertyId, [FromBody] MonthlyInvoiceRequest request, CancellationToken ct)
    {
        var result = InvoiceCreatedResponse.From(await _service.CreateMonthlyAsync(propertyId, request.ToInput(), ct));
        return StatusCode(StatusCodes.Status201Created, new ApiResponse<InvoiceCreatedResponse>(result));
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid propertyId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] Guid? roomId = null, [FromQuery] short? year = null,
        [FromQuery] byte? month = null, [FromQuery] InvoiceStatus? status = null,
        CancellationToken ct = default)
    {
        var result = await _service.ListAsync(propertyId,
            new InvoiceFilter(page, pageSize, roomId, year, month, status), ct);
        return Ok(new PageResponse<InvoiceSummaryResponse>(
            result.Items.Select(InvoiceSummaryResponse.From).ToList(),
            new PageMeta(page, pageSize, result.TotalItems,
                (int)Math.Ceiling((double)result.TotalItems / pageSize))));
    }
}
