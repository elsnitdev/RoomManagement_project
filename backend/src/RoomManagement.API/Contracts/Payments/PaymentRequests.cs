using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using RoomManagement.Business.DTOs;
using RoomManagement.Domain.Enums;

namespace RoomManagement.API.Contracts.Payments;

public sealed record DeclareTenantPaymentRequest(
    [property: JsonRequired] decimal Amount,
    [property: JsonRequired] PaymentMethod Method,
    [property: JsonRequired] DateTimeOffset PaidAt,
    [StringLength(150)] string? TransactionCode,
    [StringLength(500)] string? Note,
    [property: JsonRequired] Guid IdempotencyKey)
{
    public DeclareTenantPaymentInput ToInput() =>
        new(Amount, Method, PaidAt, TransactionCode, Note, IdempotencyKey);
}
