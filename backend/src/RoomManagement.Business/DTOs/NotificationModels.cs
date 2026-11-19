using RoomManagement.Domain.Enums;

namespace RoomManagement.Business.DTOs;

public sealed record AdminLandlordOptionDto(
    string UserId,
    string DisplayName,
    string UserName,
    string? Email,
    string? PhoneNumber,
    string? LandlordCode,
    int ActivePropertyCount);

public sealed record AdminNotificationTargetsDto(
    int TotalActiveSystemUsers,
    int TotalActiveLandlords,
    IReadOnlyList<AdminLandlordOptionDto> Landlords);

public sealed record SendAdminNotificationInput(
    AdminNotificationTargetScope Scope,
    string? TargetUserId,
    string Title,
    string Message,
    Guid IdempotencyKey);

public sealed record OperatorTenantStayDto(
    Guid PropertyId,
    string PropertyName,
    Guid RoomId,
    string RoomNumber,
    Guid ContractId);

public sealed record OperatorPropertyOptionDto(
    Guid PropertyId,
    string Name,
    string? Address,
    int ActiveTenantCount);

public sealed record OperatorTenantOptionDto(
    string UserId,
    string FullName,
    string? PhoneNumber,
    string? Email,
    IReadOnlyList<OperatorTenantStayDto> ActiveStays);

public sealed record OperatorNotificationTargetsDto(
    int TotalActiveTenants,
    IReadOnlyList<OperatorPropertyOptionDto> Properties,
    IReadOnlyList<OperatorTenantOptionDto> Tenants);

public sealed record SendOperatorNotificationInput(
    OperatorNotificationTargetScope Scope,
    Guid? PropertyId,
    string? TargetUserId,
    string Title,
    string Message,
    Guid IdempotencyKey);

public sealed record SendNotificationResultDto(
    string EventKey,
    string Type,
    int RecipientCount,
    bool Duplicate,
    DateTime SentAt);

public sealed record SentNotificationItemDto(
    string EventKey,
    string Type,
    string TargetScope,
    string TargetLabel,
    Guid? PropertyId,
    string? PropertyName,
    string Title,
    string Message,
    int TotalRecipients,
    int ReadCount,
    DateTime CreatedAt);
