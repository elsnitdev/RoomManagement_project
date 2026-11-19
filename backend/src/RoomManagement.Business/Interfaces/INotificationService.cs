using RoomManagement.Business.DTOs;

namespace RoomManagement.Business.Interfaces;

public interface INotificationService
{
    Task<AdminNotificationTargetsDto> GetAdminTargetsAsync(string actorAdminId, string? search, CancellationToken ct = default);
    Task<SendNotificationResultDto> SendAdminNotificationAsync(string actorAdminId, SendAdminNotificationInput input, CancellationToken ct = default);

    Task<OperatorNotificationTargetsDto> GetOperatorTargetsAsync(string? search, CancellationToken ct = default);
    Task<SendNotificationResultDto> SendOperatorNotificationAsync(SendOperatorNotificationInput input, CancellationToken ct = default);

    Task<PageResult<SentNotificationItemDto>> ListSentNotificationsAsync(string actorUserId, int page, int pageSize, CancellationToken ct = default);
}
