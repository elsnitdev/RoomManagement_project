using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using RoomManagement.Business.DTOs;
using RoomManagement.Domain.Enums;

namespace RoomManagement.API.Contracts.Payments;

public sealed record ReviewPaymentRequest(
    [property: JsonRequired] PaymentStatus Status,
    decimal? VerifiedAmount,
    [property: JsonRequired][Required, StringLength(500)] string Reason)
{
    public ReviewPaymentInput ToInput() => new(Status, VerifiedAmount, Reason);
}

public sealed record RecordReceivedPaymentRequest(
    [property: JsonRequired] decimal Amount,
    [property: JsonRequired] PaymentMethod Method,
    DateTimeOffset? PaidAt,
    [StringLength(150)] string? TransactionCode,
    [StringLength(500)] string? Note,
    [property: JsonRequired] Guid IdempotencyKey)
{
    public RecordReceivedPaymentInput ToInput() =>
        new(Amount, Method, PaidAt, TransactionCode, Note, IdempotencyKey);
}
