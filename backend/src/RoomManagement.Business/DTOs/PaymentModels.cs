using RoomManagement.Domain.Enums;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Business.DTOs;

public sealed record DeclareTenantPaymentInput(decimal Amount, PaymentMethod Method,
    DateTimeOffset PaidAt, string? TransactionCode, string? Note, Guid IdempotencyKey);

public sealed record ReviewPaymentInput(PaymentStatus Status, decimal? VerifiedAmount, string Reason);

public sealed record RecordReceivedPaymentInput(decimal Amount, PaymentMethod Method,
    DateTimeOffset? PaidAt, string? TransactionCode, string? Note, Guid IdempotencyKey);

public sealed record OwnerPaymentResult(Payment Payment, string? SubmittedByName,
    string? ReviewReason);

public sealed record TenantPaymentResult(Payment Payment, string? ReviewReason);
