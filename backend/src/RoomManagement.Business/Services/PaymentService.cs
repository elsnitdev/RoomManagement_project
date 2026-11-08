using RoomManagement.Business.DTOs;
using RoomManagement.Business.Errors;
using RoomManagement.Business.Interfaces;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Business.Services;

public sealed class PaymentService
{
    private readonly IPaymentStore _payments;

    public PaymentService(IPaymentStore payments) => _payments = payments;

    public Task<Payment> DeclareTenantAsync(Guid invoiceId, string tenantUserId,
        DeclareTenantPaymentInput input, CancellationToken ct)
    {
        var transactionCode = input.TransactionCode?.Trim();
        var note = input.Note?.Trim();
        if (string.IsNullOrWhiteSpace(tenantUserId) || invoiceId == Guid.Empty)
            throw new BusinessException(BusinessErrorType.NotFound,
                "INVOICE_NOT_FOUND", "Không tìm thấy hóa đơn.");
        if (input.Amount <= 0 || input.Amount > 9_999_999_999_999_999.99m ||
            decimal.Round(input.Amount, 2) != input.Amount ||
            input.Method is not (PaymentMethod.Cash or PaymentMethod.BankTransfer) ||
            input.PaidAt == default || input.PaidAt.UtcDateTime > DateTime.UtcNow ||
            input.IdempotencyKey == Guid.Empty ||
            transactionCode is { Length: > 150 } || note is { Length: > 500 })
            throw new BusinessException(BusinessErrorType.Validation,
                "PAYMENT_INVALID", "Thông tin khoản thanh toán không hợp lệ.");
        var paidAt = input.PaidAt.UtcDateTime;
        paidAt = new DateTime(paidAt.Ticks - paidAt.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);
        return _payments.DeclareTenantAsync(invoiceId, tenantUserId, input with
        {
            PaidAt = new DateTimeOffset(paidAt),
            TransactionCode = string.IsNullOrEmpty(transactionCode) ? null : transactionCode,
            Note = string.IsNullOrEmpty(note) ? null : note
        }, ct);
    }

    public Task<IReadOnlyList<Payment>> ListTenantAsync(Guid invoiceId,
        string tenantUserId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tenantUserId) || invoiceId == Guid.Empty)
            throw new BusinessException(BusinessErrorType.NotFound,
                "INVOICE_NOT_FOUND", "Không tìm thấy hóa đơn.");
        return _payments.ListTenantAsync(invoiceId, tenantUserId, ct);
    }
}
