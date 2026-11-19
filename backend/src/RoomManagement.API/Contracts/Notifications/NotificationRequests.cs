using RoomManagement.Business.DTOs;
using RoomManagement.Domain.Enums;

namespace RoomManagement.API.Contracts.Notifications;

public sealed record SendAdminNotificationRequest(
    AdminNotificationTargetScope Scope,
    string? TargetUserId,
    string Title,
    string Message,
    Guid IdempotencyKey)
{
    public SendAdminNotificationInput ToInput() =>
        new(Scope, TargetUserId, Title, Message, IdempotencyKey);
}

public sealed record SendOperatorNotificationRequest(
    OperatorNotificationTargetScope Scope,
    Guid? PropertyId,
    string? TargetUserId,
    string Title,
    string Message,
    Guid IdempotencyKey)
{
    public SendOperatorNotificationInput ToInput() =>
        new(Scope, PropertyId, TargetUserId, Title, Message, IdempotencyKey);
}

public sealed record LegacyBroadcastRequest(
    string Title,
    string Message,
    Guid IdempotencyKey);
