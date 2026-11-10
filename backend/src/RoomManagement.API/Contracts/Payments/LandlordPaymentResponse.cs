using RoomManagement.Business.DTOs;
using RoomManagement.Domain.Enums;

namespace RoomManagement.API.Contracts.Payments;

public sealed record LandlordPaymentResponse(Guid Id, Guid InvoiceId,
    decimal ClaimedAmount, decimal? VerifiedAmount, PaymentMethod Method,
    PaymentStatus Status, DateTime? PaidAt, DateTime CreatedAt,
    DateTime? VerifiedAt, string? SubmittedByUserId, string? SubmittedByName,
    string? VerifiedByUserId, string? TransactionCode, string? Note,
    string? ReviewReason)
{
    public static LandlordPaymentResponse From(OwnerPaymentResult result)
    {
        var payment = result.Payment;
        return new(payment.Id, payment.InvoiceId, payment.ClaimedAmount,
            payment.VerifiedAmount, payment.PaymentMethod, payment.Status,
            payment.PaidAt, payment.CreatedAt, payment.VerifiedAt,
            payment.SubmittedByUserId, result.SubmittedByName,
            payment.VerifiedByUserId, payment.TransactionCode, payment.Note,
            result.ReviewReason);
    }
}
