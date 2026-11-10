using RoomManagement.Business.DTOs;
using RoomManagement.Business.Errors;
using RoomManagement.Business.Interfaces;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Business.Services;

public sealed class LandlordPaymentService
{
    private readonly ICurrentLandlord _current;
    private readonly ILandlordStore _landlords;
    private readonly IPaymentStore _payments;

    public LandlordPaymentService(ICurrentLandlord current, ILandlordStore landlords,
        IPaymentStore payments)
    {
        _current = current;
        _landlords = landlords;
        _payments = payments;
    }

    private async Task<string> OwnerAsync(CancellationToken ct)
    {
        var owner = _current.GetUserId();
        if (string.IsNullOrWhiteSpace(owner) || !await _landlords.IsActiveLandlordAsync(owner, ct))
            throw new BusinessException(BusinessErrorType.InvalidOperation,
                "LANDLORD_UNAVAILABLE", "Chủ trọ không khả dụng.");
        return owner;
    }

    public async Task<IReadOnlyList<OwnerPaymentResult>> ListAsync(Guid invoiceId, CancellationToken ct)
    {
        if (invoiceId == Guid.Empty) throw NotFound();
        return await _payments.ListOwnedAsync(invoiceId, await OwnerAsync(ct), ct);
    }

    public async Task<OwnerPaymentResult> ReviewAsync(Guid paymentId,
        ReviewPaymentInput input, CancellationToken ct)
    {
        if (paymentId == Guid.Empty) throw NotFound();
        var reason = input.Reason?.Trim() ?? "";
        if (reason.Length is < 1 or > 500 ||
            input.Status is not (PaymentStatus.Success or PaymentStatus.Failed) ||
            input.Status == PaymentStatus.Success &&
                (input.VerifiedAmount is null or <= 0 or > 9_999_999_999_999_999.99m ||
                 decimal.Round(input.VerifiedAmount.Value, 2) != input.VerifiedAmount.Value) ||
            input.Status == PaymentStatus.Failed && input.VerifiedAmount is not null)
            throw new BusinessException(BusinessErrorType.Validation,
                "PAYMENT_REVIEW_INVALID", "Thông tin xác nhận thanh toán không hợp lệ.");
        return await _payments.ReviewAsync(paymentId, await OwnerAsync(ct),
            input with { Reason = reason }, ct);
    }

    public async Task<OwnerPaymentResult> RecordReceivedAsync(Guid invoiceId,
        RecordReceivedPaymentInput input, CancellationToken ct)
    {
        if (invoiceId == Guid.Empty) throw NotFound();
        var transactionCode = input.TransactionCode?.Trim();
        var note = input.Note?.Trim();
        if (input.Amount <= 0 || input.Amount > 9_999_999_999_999_999.99m ||
            decimal.Round(input.Amount, 2) != input.Amount ||
            input.Method is not (PaymentMethod.Cash or PaymentMethod.BankTransfer) ||
            input.PaidAt is { } suppliedPaidAt && (suppliedPaidAt == default || suppliedPaidAt.UtcDateTime > DateTime.UtcNow) ||
            input.IdempotencyKey == Guid.Empty ||
            transactionCode is { Length: > 150 } || note is { Length: > 500 })
            throw new BusinessException(BusinessErrorType.Validation,
                "PAYMENT_INVALID", "Thông tin khoản thanh toán không hợp lệ.");
        var paidAt = input.PaidAt?.UtcDateTime;
        if (paidAt is { } value)
            paidAt = new DateTime(value.Ticks - value.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);
        return await _payments.RecordReceivedAsync(invoiceId, await OwnerAsync(ct), input with
        {
            PaidAt = paidAt is { } timestamp ? new DateTimeOffset(timestamp) : null,
            TransactionCode = string.IsNullOrEmpty(transactionCode) ? null : transactionCode,
            Note = string.IsNullOrEmpty(note) ? null : note
        }, ct);
    }

    public async Task<RoomManagement.Domain.Entities.Refund> RecordOverpaymentRefundAsync(Guid invoiceId,
        decimal amount, string? transactionCode, Guid idempotencyKey, CancellationToken ct)
    {
        var code = transactionCode?.Trim();
        if (invoiceId == Guid.Empty || amount <= 0 || amount > 9_999_999_999_999_999.99m ||
            decimal.Round(amount, 2) != amount || idempotencyKey == Guid.Empty || code is { Length: > 150 })
            throw new BusinessException(BusinessErrorType.Validation, "REFUND_INVALID", "Thông tin hoàn tiền không hợp lệ.");
        return await _payments.RecordOverpaymentRefundAsync(invoiceId, await OwnerAsync(ct), amount,
            string.IsNullOrWhiteSpace(code) ? null : code, idempotencyKey, ct);
    }

    public async Task<IReadOnlyList<RoomManagement.Domain.Entities.Refund>> ListOverpaymentRefundsAsync(Guid invoiceId, CancellationToken ct) =>
        await _payments.ListOverpaymentRefundsAsync(invoiceId, await OwnerAsync(ct), ct);

    private static BusinessException NotFound() => new(BusinessErrorType.NotFound,
        "PAYMENT_NOT_FOUND", "Không tìm thấy khoản thanh toán.");
}
