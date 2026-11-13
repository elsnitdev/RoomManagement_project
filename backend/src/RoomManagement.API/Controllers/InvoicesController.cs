using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomManagement.API.Contracts.Common;
using RoomManagement.API.Contracts.Invoices;
using RoomManagement.Business.Services;
using RoomManagement.Domain;

namespace RoomManagement.API.Controllers;

[ApiController]
[Authorize(Roles = UserRoles.Landlord)]
[Route("api/invoices")]
public sealed class InvoicesController : ControllerBase
{
    private readonly InvoiceService _service;
    public InvoicesController(InvoiceService service) => _service = service;

    [HttpGet("{invoiceId:guid}")]
    public async Task<IActionResult> Get(Guid invoiceId, CancellationToken ct) =>
        Ok(new ApiResponse<InvoiceDetailResponse>(InvoiceDetailResponse.From(
            await _service.GetDetailAsync(invoiceId, ct))));

    [HttpPut("{invoiceId:guid}/draft-items")]
    public async Task<IActionResult> ReplaceDraftItems(Guid invoiceId,
        [FromBody] DraftInvoiceItemsRequest request, CancellationToken ct) =>
        Ok(new ApiResponse<InvoiceCreatedResponse>(InvoiceCreatedResponse.From(
            await _service.ReplaceDraftItemsAsync(invoiceId, request.ToInput(), ct))));

    [HttpGet("{invoiceId:guid}/adjustments")]
    public async Task<IActionResult> Adjustments(Guid invoiceId, CancellationToken ct) =>
        Ok(new ApiResponse<IReadOnlyList<InvoiceAdjustmentResponse>>(
            (await _service.ListAdjustmentsAsync(invoiceId, ct)).Select(InvoiceAdjustmentResponse.From).ToList()));

    [HttpPost("{invoiceId:guid}/adjustments")]
    public async Task<IActionResult> AddAdjustment(Guid invoiceId,
        [FromBody] InvoiceAdjustmentRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, new ApiResponse<InvoiceAdjustmentResponse>(
            InvoiceAdjustmentResponse.From(await _service.AddAdjustmentAsync(invoiceId, request.ToInput(), ct))));

    [HttpPost("{invoiceId:guid}/issue")]
    public async Task<IActionResult> Issue(Guid invoiceId, CancellationToken ct) =>
        Ok(new ApiResponse<InvoiceCreatedResponse>(InvoiceCreatedResponse.From(
            await _service.IssueAsync(invoiceId, ct))));

    [HttpPost("{invoiceId:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid invoiceId, CancellationToken ct) =>
        Ok(new ApiResponse<InvoiceCreatedResponse>(InvoiceCreatedResponse.From(
            await _service.CancelAsync(invoiceId, ct))));
}
