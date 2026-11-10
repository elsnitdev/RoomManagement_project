using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomManagement.API.Contracts.Common;
using RoomManagement.API.Contracts.Payments;
using RoomManagement.Business.Services;
using RoomManagement.Domain;

namespace RoomManagement.API.Controllers;

[ApiController]
[Authorize(Roles = UserRoles.Landlord)]
[Route("api")]
public sealed class LandlordPaymentsController : ControllerBase
{
    public sealed record RefundRequest(decimal Amount, string? TransactionCode, Guid IdempotencyKey);
    private readonly LandlordPaymentService _payments;

    public LandlordPaymentsController(LandlordPaymentService payments) => _payments = payments;

    [HttpGet("invoices/{invoiceId:guid}/payments")]
    public async Task<IActionResult> List(Guid invoiceId, CancellationToken ct)
    {
        var rows = await _payments.ListAsync(invoiceId, ct);
        return Ok(new ApiResponse<IReadOnlyList<LandlordPaymentResponse>>(
            rows.Select(LandlordPaymentResponse.From).ToList()));
    }

    [HttpPost("payments/{paymentId:guid}/review")]
    public async Task<IActionResult> Review(Guid paymentId,
        [FromBody] ReviewPaymentRequest request, CancellationToken ct) =>
        Ok(new ApiResponse<LandlordPaymentResponse>(LandlordPaymentResponse.From(
            await _payments.ReviewAsync(paymentId, request.ToInput(), ct))));

    [HttpPost("invoices/{invoiceId:guid}/payments/received")]
    public async Task<IActionResult> RecordReceived(Guid invoiceId,
        [FromBody] RecordReceivedPaymentRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created,
            new ApiResponse<LandlordPaymentResponse>(LandlordPaymentResponse.From(
                await _payments.RecordReceivedAsync(invoiceId, request.ToInput(), ct))));

    [HttpPost("invoices/{invoiceId:guid}/overpayment-refunds")]
    public async Task<IActionResult> RefundOverpayment(Guid invoiceId, [FromBody] RefundRequest request, CancellationToken ct)
    {
        var refund = await _payments.RecordOverpaymentRefundAsync(invoiceId, request.Amount, request.TransactionCode, request.IdempotencyKey, ct);
        return StatusCode(StatusCodes.Status201Created, new ApiResponse<object>(new
        { refund.Id, refund.InvoiceId, refund.Amount, refund.Status, refund.RefundedAt, refund.TransactionCode }));
    }

    [HttpGet("invoices/{invoiceId:guid}/overpayment-refunds")]
    public async Task<IActionResult> ListRefunds(Guid invoiceId, CancellationToken ct)
    {
        var refunds = await _payments.ListOverpaymentRefundsAsync(invoiceId, ct);
        return Ok(new ApiResponse<object>(refunds.Select(refund => new
        { refund.Id, refund.InvoiceId, refund.Amount, refund.Status, refund.RefundedAt, refund.TransactionCode }).ToList()));
    }
}
