using System.Collections.Concurrent;
using RoomManagement.Business.DTOs;
using RoomManagement.Business.Interfaces;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Tests;

internal sealed class TestRequestStore : IRequestStore
{
    public ConcurrentDictionary<Guid, Property> Properties { get; } = new();
    public ConcurrentDictionary<string, List<TenantActiveStayInfo>> ActiveStaysByTenant { get; } = new(StringComparer.Ordinal);
    public ConcurrentDictionary<Guid, List<AnnouncementRecipientOptionDto>> EligibleRecipientsByProperty { get; } = new();
    public ConcurrentDictionary<Guid, Request> Requests { get; } = new();
    public ConcurrentDictionary<Guid, RequestRecipient> Recipients { get; } = new();
    public ConcurrentDictionary<string, (string FullName, string? Phone)> UsersInfo { get; } = new(StringComparer.Ordinal);

    public Task<IReadOnlyList<TenantActiveStayInfo>> GetTenantActiveStaysAsync(
        string tenantUserId, CancellationToken ct = default)
    {
        ActiveStaysByTenant.TryGetValue(tenantUserId, out var list);
        return Task.FromResult<IReadOnlyList<TenantActiveStayInfo>>(list?.ToList() ?? []);
    }

    public Task<Property?> GetOwnedPropertyAsync(
        Guid propertyId, string landlordUserId, CancellationToken ct = default)
    {
        if (Properties.TryGetValue(propertyId, out var p) && p.OwnerUserId == landlordUserId && p.IsActive)
            return Task.FromResult<Property?>(p);
        return Task.FromResult<Property?>(null);
    }

    public Task<Request> AddRequestWithRecipientsAsync(
        Request request, IReadOnlyList<RequestRecipient> recipients, CancellationToken ct = default)
    {
        Requests[request.Id] = request;
        foreach (var rec in recipients)
        {
            Recipients[rec.Id] = rec;
        }
        return Task.FromResult(request);
    }

    public Task<(IReadOnlyList<TenantRequestItemDto> Items, long Total)> ListTenantRequestsAsync(
        string tenantUserId, RequestRecipientStatus? status, int skip, int take, CancellationToken ct = default)
    {
        var query = Requests.Values
            .Where(r => r.SenderUserId == tenantUserId && r.Type == RequestType.Request)
            .Select(req =>
            {
                var rec = Recipients.Values.First(x => x.RequestId == req.Id);
                Properties.TryGetValue(req.PropertyId, out var prop);
                var stay = ActiveStaysByTenant.GetValueOrDefault(tenantUserId)?
                    .FirstOrDefault(s => s.PropertyId == req.PropertyId);
                return new TenantRequestItemDto(
                    req.Id,
                    req.PropertyId,
                    prop?.Name ?? "Khu trọ",
                    stay?.RoomNumber ?? "P101",
                    req.Title,
                    req.Content,
                    req.CreatedAt,
                    rec.IsRead,
                    rec.ReadAt,
                    rec.Status);
            });

        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        var ordered = query.OrderByDescending(x => x.CreatedAt).ToList();
        return Task.FromResult<(IReadOnlyList<TenantRequestItemDto>, long)>(
            (ordered.Skip(skip).Take(take).ToList(), ordered.Count));
    }

    public async Task<TenantRequestItemDto?> GetTenantRequestByIdAsync(
        Guid requestId, string tenantUserId, CancellationToken ct = default)
    {
        var (items, _) = await ListTenantRequestsAsync(tenantUserId, null, 0, 1000, ct);
        return items.FirstOrDefault(x => x.Id == requestId);
    }

    public Task<(IReadOnlyList<TenantAnnouncementItemDto> Items, long Total)> ListTenantAnnouncementsAsync(
        string tenantUserId, bool? isRead, int skip, int take, CancellationToken ct = default)
    {
        var query = Recipients.Values
            .Where(rec => rec.ReceiverUserId == tenantUserId)
            .Select(rec => (rec, req: Requests[rec.RequestId]))
            .Where(x => x.req.Type == RequestType.Announcement)
            .Select(x =>
            {
                Properties.TryGetValue(x.req.PropertyId, out var prop);
                var stay = ActiveStaysByTenant.GetValueOrDefault(tenantUserId)?
                    .FirstOrDefault(s => s.PropertyId == x.req.PropertyId);
                UsersInfo.TryGetValue(x.req.SenderUserId, out var senderInfo);
                return new TenantAnnouncementItemDto(
                    x.req.Id,
                    x.rec.Id,
                    x.req.PropertyId,
                    prop?.Name ?? "Khu trọ",
                    stay?.RoomNumber ?? "P101",
                    x.req.SenderUserId,
                    senderInfo.FullName ?? "Chủ trọ",
                    x.req.RecipientType,
                    x.req.Title,
                    x.req.Content,
                    x.req.CreatedAt,
                    x.rec.IsRead,
                    x.rec.ReadAt,
                    x.rec.Status);
            });

        if (isRead.HasValue)
            query = query.Where(x => x.IsRead == isRead.Value);

        var ordered = query.OrderByDescending(x => x.CreatedAt).ToList();
        return Task.FromResult<(IReadOnlyList<TenantAnnouncementItemDto>, long)>(
            (ordered.Skip(skip).Take(take).ToList(), ordered.Count));
    }

    public async Task<TenantAnnouncementItemDto?> MarkAnnouncementAsReadAsync(
        Guid requestId, string tenantUserId, CancellationToken ct = default)
    {
        var rec = Recipients.Values.FirstOrDefault(r =>
            r.RequestId == requestId &&
            r.ReceiverUserId == tenantUserId &&
            Requests.TryGetValue(r.RequestId, out var req) &&
            req.Type == RequestType.Announcement);

        if (rec is null)
            return null;

        if (!rec.IsRead)
        {
            rec.IsRead = true;
            rec.ReadAt = DateTime.UtcNow;
        }

        var (items, _) = await ListTenantAnnouncementsAsync(tenantUserId, null, 0, 1000, ct);
        return items.FirstOrDefault(x => x.Id == requestId);
    }

    public Task<(IReadOnlyList<LandlordRequestItemDto> Items, long Total)> ListLandlordRequestsAsync(
        string landlordUserId, LandlordRequestFilter filter, int skip, int take, CancellationToken ct = default)
    {
        var query = Recipients.Values
            .Where(rec => rec.ReceiverUserId == landlordUserId)
            .Select(rec => (rec, req: Requests[rec.RequestId]))
            .Where(x =>
                x.req.Type == RequestType.Request &&
                Properties.TryGetValue(x.req.PropertyId, out var p) &&
                p.OwnerUserId == landlordUserId)
            .Select(x =>
            {
                var prop = Properties[x.req.PropertyId];
                UsersInfo.TryGetValue(x.req.SenderUserId, out var senderInfo);
                var stay = ActiveStaysByTenant.GetValueOrDefault(x.req.SenderUserId)?
                    .FirstOrDefault(s => s.PropertyId == x.req.PropertyId);
                return new LandlordRequestItemDto(
                    x.req.Id,
                    x.rec.Id,
                    x.req.PropertyId,
                    prop.Name,
                    x.req.SenderUserId,
                    senderInfo.FullName ?? "Người thuê",
                    senderInfo.Phone,
                    stay?.RoomNumber ?? "P101",
                    x.req.Title,
                    x.req.Content,
                    x.req.CreatedAt,
                    x.rec.IsRead,
                    x.rec.ReadAt,
                    x.rec.Status);
            });

        if (filter.PropertyId.HasValue)
            query = query.Where(x => x.PropertyId == filter.PropertyId.Value);
        if (filter.Status.HasValue)
            query = query.Where(x => x.Status == filter.Status.Value);
        if (filter.IsRead.HasValue)
            query = query.Where(x => x.IsRead == filter.IsRead.Value);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.Trim();
            query = query.Where(x =>
                x.Title.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                x.Content.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                x.SenderFullName.Contains(s, StringComparison.OrdinalIgnoreCase));
        }

        var ordered = query.OrderByDescending(x => x.CreatedAt).ToList();
        return Task.FromResult<(IReadOnlyList<LandlordRequestItemDto>, long)>(
            (ordered.Skip(skip).Take(take).ToList(), ordered.Count));
    }

    public async Task<LandlordRequestItemDto?> GetLandlordRequestAndMarkReadAsync(
        Guid requestId, string landlordUserId, CancellationToken ct = default)
    {
        var rec = Recipients.Values.FirstOrDefault(r =>
            r.RequestId == requestId &&
            r.ReceiverUserId == landlordUserId &&
            Requests.TryGetValue(r.RequestId, out var req) &&
            req.Type == RequestType.Request &&
            Properties.TryGetValue(req.PropertyId, out var prop) &&
            prop.OwnerUserId == landlordUserId);

        if (rec is null)
            return null;

        if (!rec.IsRead)
        {
            rec.IsRead = true;
            rec.ReadAt = DateTime.UtcNow;
        }

        var (items, _) = await ListLandlordRequestsAsync(
            landlordUserId,
            new LandlordRequestFilter(1, 1000, null, null, null, null),
            0,
            1000,
            ct);
        return items.FirstOrDefault(x => x.Id == requestId);
    }

    public async Task<LandlordRequestItemDto?> UpdateLandlordRequestStatusAsync(
        Guid requestId, string landlordUserId, RequestRecipientStatus newStatus, CancellationToken ct = default)
    {
        var rec = Recipients.Values.FirstOrDefault(r =>
            r.RequestId == requestId &&
            r.ReceiverUserId == landlordUserId &&
            Requests.TryGetValue(r.RequestId, out var req) &&
            req.Type == RequestType.Request &&
            Properties.TryGetValue(req.PropertyId, out var prop) &&
            prop.OwnerUserId == landlordUserId);

        if (rec is null)
            return null;

        if (!rec.IsRead)
        {
            rec.IsRead = true;
            rec.ReadAt = DateTime.UtcNow;
        }
        rec.Status = newStatus;

        var (items, _) = await ListLandlordRequestsAsync(
            landlordUserId,
            new LandlordRequestFilter(1, 1000, null, null, null, null),
            0,
            1000,
            ct);
        return items.FirstOrDefault(x => x.Id == requestId);
    }

    public Task<IReadOnlyList<AnnouncementRecipientOptionDto>> ListEligibleAnnouncementRecipientsAsync(
        Guid propertyId, CancellationToken ct = default)
    {
        EligibleRecipientsByProperty.TryGetValue(propertyId, out var list);
        return Task.FromResult<IReadOnlyList<AnnouncementRecipientOptionDto>>(list?.ToList() ?? []);
    }

    public Task<(IReadOnlyList<LandlordAnnouncementItemDto> Items, long Total)> ListLandlordAnnouncementsAsync(
        Guid propertyId, string landlordUserId, int skip, int take, CancellationToken ct = default)
    {
        var query = Requests.Values
            .Where(r =>
                r.PropertyId == propertyId &&
                r.Type == RequestType.Announcement &&
                Properties.TryGetValue(r.PropertyId, out var p) &&
                p.OwnerUserId == landlordUserId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(req =>
            {
                var recs = Recipients.Values.Where(x => x.RequestId == req.Id).ToList();
                var prop = Properties[req.PropertyId];
                return new LandlordAnnouncementItemDto(
                    req.Id,
                    req.PropertyId,
                    prop.Name,
                    req.RecipientType,
                    req.Title,
                    req.Content,
                    req.CreatedAt,
                    recs.Count,
                    recs.Count(x => x.IsRead));
            })
            .ToList();

        return Task.FromResult<(IReadOnlyList<LandlordAnnouncementItemDto>, long)>(
            (query.Skip(skip).Take(take).ToList(), query.Count));
    }
}

