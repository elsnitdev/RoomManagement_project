using RoomManagement.Business.DTOs;
using RoomManagement.Business.Errors;
using RoomManagement.Business.Interfaces;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Business.Services;

public sealed class RequestService : IRequestService
{
    private readonly ICurrentLandlord _currentLandlord;
    private readonly ILandlordStore _landlordStore;
    private readonly IRequestStore _store;

    public RequestService(
        ICurrentLandlord currentLandlord,
        ILandlordStore landlordStore,
        IRequestStore store)
    {
        _currentLandlord = currentLandlord;
        _landlordStore = landlordStore;
        _store = store;
    }

    private async Task<string> RequireActiveLandlordAsync(CancellationToken ct)
    {
        var ownerId = _currentLandlord.GetUserId();
        if (string.IsNullOrWhiteSpace(ownerId) || !await _landlordStore.IsActiveLandlordAsync(ownerId, ct))
            throw BusinessException.Unprocessable("Chủ trọ hiện tại không tồn tại hoặc đã ngừng hoạt động.", "LANDLORD_UNAVAILABLE");
        return ownerId;
    }

    private static (string Title, string Content) ValidateTitleAndContent(string? rawTitle, string? rawContent)
    {
        var title = rawTitle?.Trim() ?? string.Empty;
        if (title.Length is < 1 or > 250)
            throw BusinessException.BadRequest("Tiêu đề không được để trống và tối đa 250 ký tự.", "INVALID_TITLE");

        var content = rawContent?.Trim() ?? string.Empty;
        if (content.Length is < 1 or > 4000)
            throw BusinessException.BadRequest("Nội dung không được để trống và tối đa 4000 ký tự.", "INVALID_CONTENT");

        return (title, content);
    }

    private static void ValidatePagination(int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 100)
            throw BusinessException.BadRequest("Tham số phân trang không hợp lệ.", "PAGE_INVALID");
    }

    public async Task<IReadOnlyList<TenantRequestPropertyOptionDto>> GetTenantActivePropertiesAsync(
        string tenantUserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tenantUserId))
            throw BusinessException.Forbidden("Không xác định được tài khoản người thuê.", "FORBIDDEN");

        var stays = await _store.GetTenantActiveStaysAsync(tenantUserId, ct);
        return stays
            .GroupBy(s => s.PropertyId)
            .Select(g => new TenantRequestPropertyOptionDto(
                g.Key,
                g.First().PropertyName,
                g.First().RoomNumber))
            .ToList();
    }

    public async Task<TenantRequestItemDto> CreateTenantRequestAsync(
        string tenantUserId, CreateTenantRequestInput input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tenantUserId))
            throw BusinessException.Forbidden("Không xác định được tài khoản người thuê.", "FORBIDDEN");

        var (title, content) = ValidateTitleAndContent(input.Title, input.Content);
        var activeStays = await _store.GetTenantActiveStaysAsync(tenantUserId, ct);
        if (activeStays.Count == 0)
            throw BusinessException.Unprocessable("Bạn cần có hợp đồng đang hoạt động để gửi yêu cầu.", "NO_ACTIVE_CONTRACT");

        TenantActiveStayInfo targetStay;
        if (input.PropertyId.HasValue && input.PropertyId.Value != Guid.Empty)
        {
            targetStay = activeStays.FirstOrDefault(s => s.PropertyId == input.PropertyId.Value)
                ?? throw BusinessException.Unprocessable("Bạn cần có hợp đồng đang hoạt động tại khu trọ này để gửi yêu cầu.", "NO_ACTIVE_CONTRACT");
        }
        else
        {
            var distinctProps = activeStays.Select(s => s.PropertyId).Distinct().ToList();
            if (distinctProps.Count > 1)
                throw BusinessException.BadRequest("Vui lòng chọn khu trọ bạn muốn gửi yêu cầu.", "PROPERTY_REQUIRED");
            targetStay = activeStays[0];
        }

        var now = DateTime.UtcNow;
        var request = new Request
        {
            Id = Guid.NewGuid(),
            PropertyId = targetStay.PropertyId,
            SenderUserId = tenantUserId,
            RecipientType = RequestRecipientType.Owner,
            Title = title,
            Content = content,
            Type = RequestType.Request,
            CreatedAt = now
        };

        var recipient = new RequestRecipient
        {
            Id = Guid.NewGuid(),
            RequestId = request.Id,
            ReceiverUserId = targetStay.LandlordUserId,
            IsRead = false,
            ReadAt = null,
            Status = RequestRecipientStatus.Open
        };

        await _store.AddRequestWithRecipientsAsync(request, [recipient], ct);

        return new TenantRequestItemDto(
            request.Id,
            targetStay.PropertyId,
            targetStay.PropertyName,
            targetStay.RoomNumber,
            request.Title,
            request.Content,
            request.CreatedAt,
            false,
            null,
            RequestRecipientStatus.Open);
    }

    public async Task<PageResult<TenantRequestItemDto>> ListTenantRequestsAsync(
        string tenantUserId, TenantRequestFilter filter, CancellationToken ct = default)
    {
        ValidatePagination(filter.Page, filter.PageSize);
        var (items, total) = await _store.ListTenantRequestsAsync(
            tenantUserId, filter.Status, (filter.Page - 1) * filter.PageSize, filter.PageSize, ct);
        return new PageResult<TenantRequestItemDto>(items, total, filter.Page, filter.PageSize);
    }

    public async Task<TenantRequestItemDto> GetTenantRequestByIdAsync(
        string tenantUserId, Guid requestId, CancellationToken ct = default)
    {
        return await _store.GetTenantRequestByIdAsync(requestId, tenantUserId, ct)
            ?? throw BusinessException.NotFound("Không tìm thấy yêu cầu.", "REQUEST_NOT_FOUND");
    }

    public async Task<PageResult<TenantAnnouncementItemDto>> ListTenantAnnouncementsAsync(
        string tenantUserId, TenantAnnouncementFilter filter, CancellationToken ct = default)
    {
        ValidatePagination(filter.Page, filter.PageSize);
        var (items, total) = await _store.ListTenantAnnouncementsAsync(
            tenantUserId, filter.IsRead, (filter.Page - 1) * filter.PageSize, filter.PageSize, ct);
        return new PageResult<TenantAnnouncementItemDto>(items, total, filter.Page, filter.PageSize);
    }

    public async Task<TenantAnnouncementItemDto> MarkAnnouncementAsReadAsync(
        string tenantUserId, Guid requestId, CancellationToken ct = default)
    {
        return await _store.MarkAnnouncementAsReadAsync(requestId, tenantUserId, ct)
            ?? throw BusinessException.NotFound("Không tìm thấy thông báo.", "ANNOUNCEMENT_NOT_FOUND");
    }

    public async Task<PageResult<LandlordRequestItemDto>> ListLandlordRequestsAsync(
        LandlordRequestFilter filter, CancellationToken ct = default)
    {
        var landlordId = await RequireActiveLandlordAsync(ct);
        ValidatePagination(filter.Page, filter.PageSize);
        var (items, total) = await _store.ListLandlordRequestsAsync(
            landlordId, filter, (filter.Page - 1) * filter.PageSize, filter.PageSize, ct);
        return new PageResult<LandlordRequestItemDto>(items, total, filter.Page, filter.PageSize);
    }

    public async Task<LandlordRequestItemDto> GetLandlordRequestByIdAsync(Guid requestId, CancellationToken ct = default)
    {
        var landlordId = await RequireActiveLandlordAsync(ct);
        return await _store.GetLandlordRequestAndMarkReadAsync(requestId, landlordId, ct)
            ?? throw BusinessException.NotFound("Không tìm thấy yêu cầu.", "REQUEST_NOT_FOUND");
    }

    public async Task<LandlordRequestItemDto> UpdateLandlordRequestStatusAsync(
        Guid requestId, RequestRecipientStatus status, CancellationToken ct = default)
    {
        var landlordId = await RequireActiveLandlordAsync(ct);
        if (!Enum.IsDefined(status))
            throw BusinessException.BadRequest("Trạng thái xử lý không hợp lệ.", "INVALID_STATUS");

        return await _store.UpdateLandlordRequestStatusAsync(requestId, landlordId, status, ct)
            ?? throw BusinessException.NotFound("Không tìm thấy yêu cầu.", "REQUEST_NOT_FOUND");
    }

    public async Task<IReadOnlyList<AnnouncementRecipientOptionDto>> GetAnnouncementRecipientsAsync(
        Guid propertyId, CancellationToken ct = default)
    {
        var landlordId = await RequireActiveLandlordAsync(ct);
        _ = await _store.GetOwnedPropertyAsync(propertyId, landlordId, ct)
            ?? throw BusinessException.NotFound("Không tìm thấy khu trọ.", "PROPERTY_NOT_FOUND");

        return await _store.ListEligibleAnnouncementRecipientsAsync(propertyId, ct);
    }

    public async Task<LandlordAnnouncementItemDto> CreateLandlordAnnouncementAsync(
        Guid propertyId, CreateAnnouncementInput input, CancellationToken ct = default)
    {
        var landlordId = await RequireActiveLandlordAsync(ct);
        var property = await _store.GetOwnedPropertyAsync(propertyId, landlordId, ct)
            ?? throw BusinessException.NotFound("Không tìm thấy khu trọ.", "PROPERTY_NOT_FOUND");

        var (title, content) = ValidateTitleAndContent(input.Title, input.Content);

        if (input.RecipientType is not (RequestRecipientType.Single or RequestRecipientType.Multiple or RequestRecipientType.All))
            throw BusinessException.BadRequest("Phạm vi người nhận thông báo không hợp lệ.", "INVALID_RECIPIENT_TYPE");

        var eligibleRecipients = await _store.ListEligibleAnnouncementRecipientsAsync(propertyId, ct);
        var eligibleUserIds = eligibleRecipients.Select(r => r.UserId).ToHashSet(StringComparer.Ordinal);

        var targetUserIds = new HashSet<string>(StringComparer.Ordinal);

        if (input.RecipientType == RequestRecipientType.All)
        {
            foreach (var uid in eligibleUserIds)
                targetUserIds.Add(uid);

            if (targetUserIds.Count == 0)
                throw BusinessException.Unprocessable("Khu trọ chưa có người thuê nào kích hoạt tài khoản để nhận thông báo.", "NO_ELIGIBLE_RECIPIENTS");
        }
        else if (input.RecipientType == RequestRecipientType.Single)
        {
            var requestedUsers = (input.ReceiverUserIds ?? [])
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .Select(u => u.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var requestedRooms = (input.RoomIds ?? [])
                .Where(r => r != Guid.Empty)
                .Distinct()
                .ToList();

            if (requestedUsers.Count == 0 && requestedRooms.Count == 1)
            {
                foreach (var r in eligibleRecipients.Where(x => x.RoomId == requestedRooms[0]))
                    targetUserIds.Add(r.UserId);
                if (targetUserIds.Count == 0)
                    throw BusinessException.BadRequest("Phòng được chọn không có người thuê nào kích hoạt tài khoản.", "INVALID_RECIPIENTS");
            }
            else if (requestedUsers.Count == 1)
            {
                if (!eligibleUserIds.Contains(requestedUsers[0]))
                    throw BusinessException.BadRequest("Người nhận không thuộc danh sách người thuê đang ở tại khu trọ này.", "INVALID_RECIPIENTS");
                targetUserIds.Add(requestedUsers[0]);
            }
            else
            {
                throw BusinessException.BadRequest("Chế độ gửi đơn lẻ yêu cầu chọn đúng 1 người thuê.", "INVALID_RECIPIENTS");
            }
        }
        else if (input.RecipientType == RequestRecipientType.Multiple)
        {
            var requestedUsers = (input.ReceiverUserIds ?? [])
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .Select(u => u.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var requestedRooms = (input.RoomIds ?? [])
                .Where(r => r != Guid.Empty)
                .Distinct()
                .ToList();

            foreach (var uid in requestedUsers)
            {
                if (!eligibleUserIds.Contains(uid))
                    throw BusinessException.BadRequest("Có người nhận không thuộc danh sách người thuê đang ở tại khu trọ này.", "INVALID_RECIPIENTS");
                targetUserIds.Add(uid);
            }

            if (requestedRooms.Count > 0)
            {
                var roomSet = requestedRooms.ToHashSet();
                foreach (var r in eligibleRecipients.Where(x => roomSet.Contains(x.RoomId)))
                    targetUserIds.Add(r.UserId);
            }

            if (targetUserIds.Count == 0)
                throw BusinessException.BadRequest("Vui lòng chọn ít nhất một người nhận hợp lệ trong khu trọ.", "INVALID_RECIPIENTS");
        }

        var now = DateTime.UtcNow;
        var request = new Request
        {
            Id = Guid.NewGuid(),
            PropertyId = property.Id,
            SenderUserId = landlordId,
            RecipientType = input.RecipientType,
            Title = title,
            Content = content,
            Type = RequestType.Announcement,
            CreatedAt = now
        };

        var recipients = targetUserIds
            .Select(uid => new RequestRecipient
            {
                Id = Guid.NewGuid(),
                RequestId = request.Id,
                ReceiverUserId = uid,
                IsRead = false,
                ReadAt = null,
                Status = RequestRecipientStatus.Open
            })
            .ToList();

        await _store.AddRequestWithRecipientsAsync(request, recipients, ct);

        return new LandlordAnnouncementItemDto(
            request.Id,
            property.Id,
            property.Name,
            request.RecipientType,
            request.Title,
            request.Content,
            request.CreatedAt,
            recipients.Count,
            0);
    }

    public async Task<PageResult<LandlordAnnouncementItemDto>> ListLandlordAnnouncementsAsync(
        Guid propertyId, int page, int pageSize, CancellationToken ct = default)
    {
        var landlordId = await RequireActiveLandlordAsync(ct);
        ValidatePagination(page, pageSize);
        _ = await _store.GetOwnedPropertyAsync(propertyId, landlordId, ct)
            ?? throw BusinessException.NotFound("Không tìm thấy khu trọ.", "PROPERTY_NOT_FOUND");

        var (items, total) = await _store.ListLandlordAnnouncementsAsync(
            propertyId, landlordId, (page - 1) * pageSize, pageSize, ct);
        return new PageResult<LandlordAnnouncementItemDto>(items, total, page, pageSize);
    }
}

