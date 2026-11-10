using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.API.Contracts.Payments;

public sealed record PaymentResponse(Guid Id, Guid InvoiceId, decimal ClaimedAmount,
    decimal? VerifiedAmount, PaymentMethod Method, PaymentStatus Status,
    DateTime? PaidAt, DateTime CreatedAt, string? TransactionCode, string? Note,
    string? ReviewReason = null)
{
    public static PaymentResponse From(Payment payment, string? reviewReason = null) => new(payment.Id, payment.InvoiceId,
        payment.ClaimedAmount, payment.VerifiedAmount, payment.PaymentMethod,
        payment.Status, payment.PaidAt, payment.CreatedAt,
        payment.TransactionCode, payment.Note, reviewReason);
}
