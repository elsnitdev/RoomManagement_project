using RoomManagement.Business.DTOs;
using RoomManagement.Business.Errors;
using RoomManagement.Business.Interfaces;
using RoomManagement.Domain.Constants;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Business.Services;

public sealed class NotificationService : INotificationService
{
    private readonly ICurrentLandlord _currentLandlord;
    private readonly ILandlordStore _landlordStore;
    private readonly INotificationStore _store;

    public NotificationService(
        ICurrentLandlord currentLandlord,
        ILandlordStore landlordStore,
        INotificationStore store)
    {
        _currentLandlord = currentLandlord;
        _landlordStore = landlordStore;
        _store = store;
    }

    public async Task<AdminNotificationTargetsDto> GetAdminTargetsAsync(
        string actorAdminId, string? search, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(actorAdminId))
            throw BusinessException.Forbidden("Không xác định được tài khoản quản trị.", "FORBIDDEN");

        return await _store.GetAdminTargetsAsync(actorAdminId, search?.Trim(), ct);
    }

    public async Task<SendNotificationResultDto> SendAdminNotificationAsync(
        string actorAdminId, SendAdminNotificationInput input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(actorAdminId))
            throw BusinessException.Forbidden("Không xác định được tài khoản quản trị.", "FORBIDDEN");

        if (!Enum.IsDefined(input.Scope))
            throw BusinessException.BadRequest("Phạm vi người nhận thông báo không hợp lệ.", "INVALID_SCOPE");

        var (title, message) = ValidateMessagePayload(input.Title, input.Message, input.IdempotencyKey);
        var eventKey = $"admin-notif:{input.IdempotencyKey:N}";
        var notificationType = input.Scope switch
        {
            AdminNotificationTargetScope.AllSystem => NotificationTypes.SystemBroadcast,
            AdminNotificationTargetScope.AllLandlords => NotificationTypes.AdminLandlordBroadcast,
            AdminNotificationTargetScope.SpecificLandlord => NotificationTypes.AdminDirectLandlord,
            _ => throw BusinessException.BadRequest("Phạm vi người nhận thông báo không hợp lệ.", "INVALID_SCOPE")
        };

        if (await _store.EventKeyExistsAsync(eventKey, ct))
            return new SendNotificationResultDto(eventKey, notificationType, 0, Duplicate: true, DateTime.UtcNow);

        IReadOnlyList<string> recipientIds;
        switch (input.Scope)
        {
            case AdminNotificationTargetScope.AllSystem:
                recipientIds = (await _store.GetActiveSystemUserIdsAsync(actorAdminId, ct))
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                if (recipientIds.Count == 0)
                {
                    throw BusinessException.Unprocessable(
                        "Hiện chưa có người dùng nào đang hoạt động để nhận thông báo.",
                        "NO_ELIGIBLE_RECIPIENTS");
                }
                break;

            case AdminNotificationTargetScope.AllLandlords:
                recipientIds = (await _store.GetActiveLandlordUserIdsAsync(ct))
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                if (recipientIds.Count == 0)
                {
                    throw BusinessException.Unprocessable(
                        "Hiện chưa có chủ trọ nào đang hoạt động để nhận thông báo.",
                        "NO_ELIGIBLE_RECIPIENTS");
                }
                break;

            case AdminNotificationTargetScope.SpecificLandlord:
                var targetUserId = input.TargetUserId?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(targetUserId))
                {
                    throw BusinessException.BadRequest(
                        "Vui lòng chọn một chủ trọ để gửi thông báo.",
                        "RECIPIENT_REQUIRED");
                }
                if (!await _store.IsActiveApprovedLandlordAsync(targetUserId, ct))
                {
                    throw BusinessException.Unprocessable(
                        "Chủ trọ được chọn không tồn tại, chưa được duyệt hoặc đã bị khóa.",
                        "INVALID_RECIPIENT");
                }
                recipientIds = [targetUserId];
                break;

            default:
                throw BusinessException.BadRequest("Phạm vi người nhận thông báo không hợp lệ.", "INVALID_SCOPE");
        }

        return await PersistBatchAsync(
            recipientIds,
            actorAdminId,
            notificationType,
            title,
            message,
            propertyId: null,
            eventKey,
            ct);
    }

    public async Task<OperatorNotificationTargetsDto> GetOperatorTargetsAsync(
        string? search, CancellationToken ct = default)
    {
        var context = await OperatorAccessGuard.RequireOperatorAsync(_currentLandlord, _landlordStore, ct);
        return await _store.GetOperatorTargetsAsync(context, search?.Trim(), ct);
    }

    public async Task<SendNotificationResultDto> SendOperatorNotificationAsync(
        SendOperatorNotificationInput input, CancellationToken ct = default)
    {
        var context = await OperatorAccessGuard.RequireOperatorAsync(_currentLandlord, _landlordStore, ct);

        if (!Enum.IsDefined(input.Scope))
            throw BusinessException.BadRequest("Phạm vi người nhận thông báo không hợp lệ.", "INVALID_SCOPE");

        var (title, message) = ValidateMessagePayload(input.Title, input.Message, input.IdempotencyKey);
        var eventKey = $"operator-notif:{input.IdempotencyKey:N}";
        var notificationType = input.Scope switch
        {
            OperatorNotificationTargetScope.AllActiveTenants => NotificationTypes.OperatorAllTenants,
            OperatorNotificationTargetScope.SpecificProperty => NotificationTypes.PropertyAnnouncement,
            OperatorNotificationTargetScope.SpecificTenant => NotificationTypes.OperatorDirectTenant,
            _ => throw BusinessException.BadRequest("Phạm vi người nhận thông báo không hợp lệ.", "INVALID_SCOPE")
        };

        if (await _store.EventKeyExistsAsync(eventKey, ct))
            return new SendNotificationResultDto(eventKey, notificationType, 0, Duplicate: true, DateTime.UtcNow);

        IReadOnlyList<string> recipientIds;
        Guid? resolvedPropertyId = null;

        switch (input.Scope)
        {
            case OperatorNotificationTargetScope.AllActiveTenants:
                recipientIds = (await _store.GetActiveTenantUserIdsForOperatorAsync(context, propertyId: null, ct))
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                if (recipientIds.Count == 0)
                {
                    throw BusinessException.Unprocessable(
                        "Hiện chưa có người thuê nào có hợp đồng đang hoạt động và đã liên kết tài khoản.",
                        "NO_ELIGIBLE_RECIPIENTS");
                }
                break;

            case OperatorNotificationTargetScope.SpecificProperty:
                if (input.PropertyId is not { } pid || pid == Guid.Empty)
                {
                    throw BusinessException.BadRequest(
                        "Vui lòng chọn khu trọ để gửi thông báo.",
                        "PROPERTY_REQUIRED");
                }
                if (!await _store.IsActivePropertyAccessibleAsync(context, pid, ct))
                {
                    throw BusinessException.NotFound(
                        "Không tìm thấy khu trọ hoặc bạn không có quyền quản lý khu trọ này.",
                        "PROPERTY_NOT_FOUND");
                }
                recipientIds = (await _store.GetActiveTenantUserIdsForOperatorAsync(context, pid, ct))
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                if (recipientIds.Count == 0)
                {
                    throw BusinessException.Unprocessable(
                        "Khu trọ này hiện chưa có người thuê nào có hợp đồng đang hoạt động đã liên kết tài khoản.",
                        "NO_ELIGIBLE_RECIPIENTS");
                }
                resolvedPropertyId = pid;
                break;

            case OperatorNotificationTargetScope.SpecificTenant:
                var targetTenantId = input.TargetUserId?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(targetTenantId))
                {
                    throw BusinessException.BadRequest(
                        "Vui lòng chọn một người thuê để gửi thông báo.",
                        "RECIPIENT_REQUIRED");
                }

                Guid? preferredPropertyId = input.PropertyId is { } pref && pref != Guid.Empty ? pref : null;
                if (preferredPropertyId.HasValue &&
                    !await _store.IsActivePropertyAccessibleAsync(context, preferredPropertyId.Value, ct))
                {
                    throw BusinessException.NotFound(
                        "Không tìm thấy khu trọ hoặc bạn không có quyền quản lý khu trọ này.",
                        "PROPERTY_NOT_FOUND");
                }

                resolvedPropertyId = await _store.ResolveActiveTenantPropertyIdForOperatorAsync(
                    context, targetTenantId, preferredPropertyId, ct);
                if (!resolvedPropertyId.HasValue)
                {
                    throw BusinessException.Unprocessable(
                        "Người thuê được chọn không có hợp đồng đang hoạt động trong phạm vi quản lý của bạn.",
                        "INVALID_RECIPIENT");
                }

                recipientIds = [targetTenantId];
                break;

            default:
                throw BusinessException.BadRequest("Phạm vi người nhận thông báo không hợp lệ.", "INVALID_SCOPE");
        }

        return await PersistBatchAsync(
            recipientIds,
            context.ActorUserId,
            notificationType,
            title,
            message,
            resolvedPropertyId,
            eventKey,
            ct);
    }

    public async Task<PageResult<SentNotificationItemDto>> ListSentNotificationsAsync(
        string actorUserId, int page, int pageSize, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(actorUserId))
            throw BusinessException.Forbidden("Không xác định được tài khoản người dùng.", "FORBIDDEN");

        if (page < 1 || pageSize is < 1 or > 100)
            throw BusinessException.BadRequest("Tham số phân trang không hợp lệ.", "PAGE_INVALID");

        var (items, total) = await _store.ListSentNotificationsAsync(
            actorUserId,
            (page - 1) * pageSize,
            pageSize,
            ct);

        return new PageResult<SentNotificationItemDto>(items, total, page, pageSize);
    }

    private async Task<SendNotificationResultDto> PersistBatchAsync(
        IReadOnlyList<string> recipientIds,
        string actorUserId,
        string notificationType,
        string title,
        string message,
        Guid? propertyId,
        string eventKey,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var rows = recipientIds
            .Select(uid => new UserNotification
            {
                Id = Guid.NewGuid(),
                UserId = uid,
                ActorUserId = actorUserId,
                Type = notificationType,
                Title = title,
                Message = message,
                PropertyId = propertyId,
                EventKey = eventKey,
                IsRead = false,
                CreatedAt = now
            })
            .ToList();

        var inserted = await _store.AddNotificationsBatchAsync(rows, ct);
        if (!inserted)
            return new SendNotificationResultDto(eventKey, notificationType, 0, Duplicate: true, now);

        return new SendNotificationResultDto(eventKey, notificationType, rows.Count, Duplicate: false, now);
    }

    private static (string Title, string Message) ValidateMessagePayload(
        string? rawTitle, string? rawMessage, Guid idempotencyKey)
    {
        if (idempotencyKey == Guid.Empty)
            throw BusinessException.BadRequest("Khóa chống gửi trùng không hợp lệ.", "INVALID_IDEMPOTENCY_KEY");

        var title = rawTitle?.Trim() ?? string.Empty;
        if (title.Length is < 1 or > 250)
            throw BusinessException.BadRequest("Tiêu đề không được để trống và tối đa 250 ký tự.", "INVALID_TITLE");

        var message = rawMessage?.Trim() ?? string.Empty;
        if (message.Length is < 1 or > 4000)
            throw BusinessException.BadRequest("Nội dung không được để trống và tối đa 4000 ký tự.", "INVALID_MESSAGE");

        return (title, message);
    }
}
