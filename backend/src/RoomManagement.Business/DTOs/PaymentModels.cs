using RoomManagement.Domain.Enums;

namespace RoomManagement.Business.DTOs;

public sealed record DeclareTenantPaymentInput(decimal Amount, PaymentMethod Method,
    DateTimeOffset PaidAt, string? TransactionCode, string? Note, Guid IdempotencyKey);
