using RoomManagement.Business.DTOs;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Business.Interfaces;

public interface INotificationStore
{
    Task<AdminNotificationTargetsDto> GetAdminTargetsAsync(string actorAdminId, string? search, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetActiveSystemUserIdsAsync(string excludeUserId, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetActiveLandlordUserIdsAsync(CancellationToken ct = default);
    Task<bool> IsActiveApprovedLandlordAsync(string userId, CancellationToken ct = default);

    Task<OperatorNotificationTargetsDto> GetOperatorTargetsAsync(OperatorContext context, string? search, CancellationToken ct = default);
    Task<bool> IsActivePropertyAccessibleAsync(OperatorContext context, Guid propertyId, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetActiveTenantUserIdsForOperatorAsync(OperatorContext context, Guid? propertyId, CancellationToken ct = default);
    Task<Guid?> ResolveActiveTenantPropertyIdForOperatorAsync(
        OperatorContext context, string tenantUserId, Guid? preferredPropertyId, CancellationToken ct = default);

    Task<bool> EventKeyExistsAsync(string eventKey, CancellationToken ct = default);
    Task<bool> AddNotificationsBatchAsync(IReadOnlyList<UserNotification> notifications, CancellationToken ct = default);
    Task<(IReadOnlyList<SentNotificationItemDto> Items, long Total)> ListSentNotificationsAsync(
        string actorUserId, int skip, int take, CancellationToken ct = default);
}
