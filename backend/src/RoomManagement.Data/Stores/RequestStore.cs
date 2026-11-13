using Microsoft.EntityFrameworkCore;
using RoomManagement.Business.DTOs;
using RoomManagement.Business.Interfaces;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Data.Stores;

public sealed class RequestStore : IRequestStore
{
    private readonly RoomManagementDbContext _db;

    public RequestStore(RoomManagementDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<TenantActiveStayInfo>> GetTenantActiveStaysAsync(
        string tenantUserId, CancellationToken ct = default)
    {
        return await (
            from profile in _db.TenantProfiles.AsNoTracking()
            join link in _db.ContractTenants.AsNoTracking() on profile.Id equals link.TenantId
            join contract in _db.Contracts.AsNoTracking() on link.ContractId equals contract.Id
            join room in _db.Rooms.AsNoTracking() on contract.RoomId equals room.Id
            join property in _db.Properties.AsNoTracking() on room.PropertyId equals property.Id
            where profile.UserId == tenantUserId &&
                  link.LeaveDate == null &&
                  contract.Status == ContractStatus.Active &&
                  property.IsActive
            orderby contract.StartDate descending, contract.CreatedAt descending
            select new TenantActiveStayInfo(
                property.Id,
                property.Name,
                property.OwnerUserId,
                room.Id,
                room.RoomNumber,
                contract.Id)
        ).ToListAsync(ct);
    }

    public Task<Property?> GetOwnedPropertyAsync(
        Guid propertyId, string landlordUserId, CancellationToken ct = default)
    {
        return _db.Properties.AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == propertyId && p.OwnerUserId == landlordUserId && p.IsActive, ct);
    }

    public async Task<Request> AddRequestWithRecipientsAsync(
        Request request, IReadOnlyList<RequestRecipient> recipients, CancellationToken ct = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        _db.Requests.Add(request);
        if (recipients.Count > 0)
        {
            _db.RequestRecipients.AddRange(recipients);
        }
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return request;
    }

    public async Task<(IReadOnlyList<TenantRequestItemDto> Items, long Total)> ListTenantRequestsAsync(
        string tenantUserId, RequestRecipientStatus? status, int skip, int take, CancellationToken ct = default)
    {
        var query =
            from req in _db.Requests.AsNoTracking()
            join rec in _db.RequestRecipients.AsNoTracking() on req.Id equals rec.RequestId
            join prop in _db.Properties.AsNoTracking() on req.PropertyId equals prop.Id
            where req.SenderUserId == tenantUserId && req.Type == RequestType.Request
            select new { req, rec, PropertyName = prop.Name };

        if (status.HasValue)
        {
            query = query.Where(x => x.rec.Status == status.Value);
        }

        var total = await query.LongCountAsync(ct);
        var rows = await query
            .OrderByDescending(x => x.req.CreatedAt)
            .ThenByDescending(x => x.req.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

        if (rows.Count == 0)
            return ([], total);

        var propertyIds = rows.Select(r => r.req.PropertyId).Distinct().ToArray();
        var roomMap = await GetUserRoomByPropertyAsync([tenantUserId], propertyIds, ct);

        var items = rows.Select(r =>
        {
            var roomNumber = roomMap.GetValueOrDefault((tenantUserId, r.req.PropertyId)) ?? "—";
            return new TenantRequestItemDto(
                r.req.Id,
                r.req.PropertyId,
                r.PropertyName,
                roomNumber,
                r.req.Title,
                r.req.Content,
                r.req.CreatedAt,
                r.rec.IsRead,
                r.rec.ReadAt,
                r.rec.Status);
        }).ToList();

        return (items, total);
    }

    public async Task<TenantRequestItemDto?> GetTenantRequestByIdAsync(
        Guid requestId, string tenantUserId, CancellationToken ct = default)
    {
        var row = await (
            from req in _db.Requests.AsNoTracking()
            join rec in _db.RequestRecipients.AsNoTracking() on req.Id equals rec.RequestId
            join prop in _db.Properties.AsNoTracking() on req.PropertyId equals prop.Id
            where req.Id == requestId && req.SenderUserId == tenantUserId && req.Type == RequestType.Request
            select new { req, rec, PropertyName = prop.Name }
        ).SingleOrDefaultAsync(ct);

        if (row is null)
            return null;

        var roomMap = await GetUserRoomByPropertyAsync([tenantUserId], [row.req.PropertyId], ct);
        var roomNumber = roomMap.GetValueOrDefault((tenantUserId, row.req.PropertyId)) ?? "—";

        return new TenantRequestItemDto(
            row.req.Id,
            row.req.PropertyId,
            row.PropertyName,
            roomNumber,
            row.req.Title,
            row.req.Content,
            row.req.CreatedAt,
            row.rec.IsRead,
            row.rec.ReadAt,
            row.rec.Status);
    }

    public async Task<(IReadOnlyList<TenantAnnouncementItemDto> Items, long Total)> ListTenantAnnouncementsAsync(
        string tenantUserId, bool? isRead, int skip, int take, CancellationToken ct = default)
    {
        var query =
            from rec in _db.RequestRecipients.AsNoTracking()
            join req in _db.Requests.AsNoTracking() on rec.RequestId equals req.Id
            join prop in _db.Properties.AsNoTracking() on req.PropertyId equals prop.Id
            join sender in _db.Users.AsNoTracking() on req.SenderUserId equals sender.Id
            where rec.ReceiverUserId == tenantUserId && req.Type == RequestType.Announcement
            select new
            {
                req,
                rec,
                PropertyName = prop.Name,
                SenderName = sender.FullName ?? sender.UserName
            };

        if (isRead.HasValue)
        {
            query = query.Where(x => x.rec.IsRead == isRead.Value);
        }

        var total = await query.LongCountAsync(ct);
        var rows = await query
            .OrderByDescending(x => x.req.CreatedAt)
            .ThenByDescending(x => x.req.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

        if (rows.Count == 0)
            return ([], total);

        var propertyIds = rows.Select(r => r.req.PropertyId).Distinct().ToArray();
        var roomMap = await GetUserRoomByPropertyAsync([tenantUserId], propertyIds, ct);

        var items = rows.Select(r =>
        {
            var roomNumber = roomMap.GetValueOrDefault((tenantUserId, r.req.PropertyId)) ?? "—";
            return new TenantAnnouncementItemDto(
                r.req.Id,
                r.rec.Id,
                r.req.PropertyId,
                r.PropertyName,
                roomNumber,
                r.req.SenderUserId,
                r.SenderName,
                r.req.RecipientType,
                r.req.Title,
                r.req.Content,
                r.req.CreatedAt,
                r.rec.IsRead,
                r.rec.ReadAt,
                r.rec.Status);
        }).ToList();

        return (items, total);
    }

    public async Task<TenantAnnouncementItemDto?> MarkAnnouncementAsReadAsync(
        Guid requestId, string tenantUserId, CancellationToken ct = default)
    {
        var rec = await (
            from r in _db.RequestRecipients
            join req in _db.Requests on r.RequestId equals req.Id
            where req.Id == requestId && r.ReceiverUserId == tenantUserId && req.Type == RequestType.Announcement
            select r
        ).SingleOrDefaultAsync(ct);

        if (rec is null)
            return null;

        if (!rec.IsRead)
        {
            rec.IsRead = true;
            rec.ReadAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        var info = await (
            from req in _db.Requests.AsNoTracking()
            join prop in _db.Properties.AsNoTracking() on req.PropertyId equals prop.Id
            join sender in _db.Users.AsNoTracking() on req.SenderUserId equals sender.Id
            where req.Id == requestId
            select new
            {
                req,
                PropertyName = prop.Name,
                SenderName = sender.FullName ?? sender.UserName
            }
        ).SingleAsync(ct);

        var roomMap = await GetUserRoomByPropertyAsync([tenantUserId], [info.req.PropertyId], ct);
        var roomNumber = roomMap.GetValueOrDefault((tenantUserId, info.req.PropertyId)) ?? "—";

        return new TenantAnnouncementItemDto(
            info.req.Id,
            rec.Id,
            info.req.PropertyId,
            info.PropertyName,
            roomNumber,
            info.req.SenderUserId,
            info.SenderName,
            info.req.RecipientType,
            info.req.Title,
            info.req.Content,
            info.req.CreatedAt,
            rec.IsRead,
            rec.ReadAt,
            rec.Status);
    }

    public async Task<(IReadOnlyList<LandlordRequestItemDto> Items, long Total)> ListLandlordRequestsAsync(
        string landlordUserId, LandlordRequestFilter filter, int skip, int take, CancellationToken ct = default)
    {
        var query =
            from rec in _db.RequestRecipients.AsNoTracking()
            join req in _db.Requests.AsNoTracking() on rec.RequestId equals req.Id
            join prop in _db.Properties.AsNoTracking() on req.PropertyId equals prop.Id
            join sender in _db.Users.AsNoTracking() on req.SenderUserId equals sender.Id
            where rec.ReceiverUserId == landlordUserId &&
                  prop.OwnerUserId == landlordUserId &&
                  req.Type == RequestType.Request
            select new
            {
                req,
                rec,
                PropertyName = prop.Name,
                SenderFullName = sender.FullName ?? sender.UserName,
                SenderPhone = sender.PhoneNumber
            };

        if (filter.PropertyId.HasValue && filter.PropertyId.Value != Guid.Empty)
        {
            query = query.Where(x => x.req.PropertyId == filter.PropertyId.Value);
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(x => x.rec.Status == filter.Status.Value);
        }

        if (filter.IsRead.HasValue)
        {
            query = query.Where(x => x.rec.IsRead == filter.IsRead.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(x =>
                x.req.Title.Contains(term) ||
                x.req.Content.Contains(term) ||
                x.SenderFullName.Contains(term) ||
                (x.SenderPhone != null && x.SenderPhone.Contains(term)));
        }

        var total = await query.LongCountAsync(ct);
        var rows = await query
            .OrderByDescending(x => x.req.CreatedAt)
            .ThenByDescending(x => x.req.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

        if (rows.Count == 0)
            return ([], total);

        var senderIds = rows.Select(r => r.req.SenderUserId).Distinct().ToArray();
        var propertyIds = rows.Select(r => r.req.PropertyId).Distinct().ToArray();
        var roomMap = await GetUserRoomByPropertyAsync(senderIds, propertyIds, ct);
        var profileMap = await _db.TenantProfiles.AsNoTracking()
            .Where(p => p.UserId != null && senderIds.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId!, p => new { p.FullName, p.PhoneNumber }, ct);

        var items = rows.Select(r =>
        {
            var roomName = roomMap.GetValueOrDefault((r.req.SenderUserId, r.req.PropertyId)) ?? "—";
            profileMap.TryGetValue(r.req.SenderUserId, out var prof);
            var fullName = !string.IsNullOrWhiteSpace(prof?.FullName) ? prof.FullName : r.SenderFullName;
            var phone = !string.IsNullOrWhiteSpace(prof?.PhoneNumber) ? prof.PhoneNumber : r.SenderPhone;

            return new LandlordRequestItemDto(
                r.req.Id,
                r.rec.Id,
                r.req.PropertyId,
                r.PropertyName,
                r.req.SenderUserId,
                fullName,
                phone,
                roomName,
                r.req.Title,
                r.req.Content,
                r.req.CreatedAt,
                r.rec.IsRead,
                r.rec.ReadAt,
                r.rec.Status);
        }).ToList();

        return (items, total);
    }

    public async Task<LandlordRequestItemDto?> GetLandlordRequestAndMarkReadAsync(
        Guid requestId, string landlordUserId, CancellationToken ct = default)
    {
        var rec = await (
            from r in _db.RequestRecipients
            join req in _db.Requests on r.RequestId equals req.Id
            join prop in _db.Properties on req.PropertyId equals prop.Id
            where req.Id == requestId &&
                  r.ReceiverUserId == landlordUserId &&
                  prop.OwnerUserId == landlordUserId &&
                  req.Type == RequestType.Request
            select r
        ).SingleOrDefaultAsync(ct);

        if (rec is null)
            return null;

        if (!rec.IsRead)
        {
            rec.IsRead = true;
            rec.ReadAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        return await BuildLandlordRequestDtoAsync(requestId, rec, ct);
    }

    public async Task<LandlordRequestItemDto?> UpdateLandlordRequestStatusAsync(
        Guid requestId, string landlordUserId, RequestRecipientStatus newStatus, CancellationToken ct = default)
    {
        var rec = await (
            from r in _db.RequestRecipients
            join req in _db.Requests on r.RequestId equals req.Id
            join prop in _db.Properties on req.PropertyId equals prop.Id
            where req.Id == requestId &&
                  r.ReceiverUserId == landlordUserId &&
                  prop.OwnerUserId == landlordUserId &&
                  req.Type == RequestType.Request
            select r
        ).SingleOrDefaultAsync(ct);

        if (rec is null)
            return null;

        if (!rec.IsRead)
        {
            rec.IsRead = true;
            rec.ReadAt = DateTime.UtcNow;
        }
        rec.Status = newStatus;
        await _db.SaveChangesAsync(ct);

        return await BuildLandlordRequestDtoAsync(requestId, rec, ct);
    }

    private async Task<LandlordRequestItemDto> BuildLandlordRequestDtoAsync(
        Guid requestId, RequestRecipient rec, CancellationToken ct)
    {
        var row = await (
            from req in _db.Requests.AsNoTracking()
            join prop in _db.Properties.AsNoTracking() on req.PropertyId equals prop.Id
            join sender in _db.Users.AsNoTracking() on req.SenderUserId equals sender.Id
            where req.Id == requestId
            select new
            {
                req,
                PropertyName = prop.Name,
                SenderFullName = sender.FullName ?? sender.UserName,
                SenderPhone = sender.PhoneNumber
            }
        ).SingleAsync(ct);

        var roomMap = await GetUserRoomByPropertyAsync([row.req.SenderUserId], [row.req.PropertyId], ct);
        var roomName = roomMap.GetValueOrDefault((row.req.SenderUserId, row.req.PropertyId)) ?? "—";
        var prof = await _db.TenantProfiles.AsNoTracking()
            .Where(p => p.UserId == row.req.SenderUserId)
            .Select(p => new { p.FullName, p.PhoneNumber })
            .FirstOrDefaultAsync(ct);

        var fullName = !string.IsNullOrWhiteSpace(prof?.FullName) ? prof.FullName : row.SenderFullName;
        var phone = !string.IsNullOrWhiteSpace(prof?.PhoneNumber) ? prof.PhoneNumber : row.SenderPhone;

        return new LandlordRequestItemDto(
            row.req.Id,
            rec.Id,
            row.req.PropertyId,
            row.PropertyName,
            row.req.SenderUserId,
            fullName,
            phone,
            roomName,
            row.req.Title,
            row.req.Content,
            row.req.CreatedAt,
            rec.IsRead,
            rec.ReadAt,
            rec.Status);
    }

    public async Task<IReadOnlyList<AnnouncementRecipientOptionDto>> ListEligibleAnnouncementRecipientsAsync(
        Guid propertyId, CancellationToken ct = default)
    {
        var raw = await (
            from contract in _db.Contracts.AsNoTracking()
            join room in _db.Rooms.AsNoTracking() on contract.RoomId equals room.Id
            join link in _db.ContractTenants.AsNoTracking() on contract.Id equals link.ContractId
            join profile in _db.TenantProfiles.AsNoTracking() on link.TenantId equals profile.Id
            where room.PropertyId == propertyId &&
                  contract.Status == ContractStatus.Active &&
                  link.LeaveDate == null &&
                  profile.UserId != null
            orderby room.RoomNumber, profile.FullName
            select new AnnouncementRecipientOptionDto(
                profile.UserId!,
                profile.Id,
                profile.FullName,
                profile.PhoneNumber,
                room.Id,
                room.RoomNumber,
                contract.Id)
        ).ToListAsync(ct);

        return raw
            .GroupBy(x => x.UserId, StringComparer.Ordinal)
            .Select(g => g.First())
            .ToList();
    }

    public async Task<(IReadOnlyList<LandlordAnnouncementItemDto> Items, long Total)> ListLandlordAnnouncementsAsync(
        Guid propertyId, string landlordUserId, int skip, int take, CancellationToken ct = default)
    {
        var query =
            from req in _db.Requests.AsNoTracking()
            join prop in _db.Properties.AsNoTracking() on req.PropertyId equals prop.Id
            where req.PropertyId == propertyId &&
                  prop.OwnerUserId == landlordUserId &&
                  req.Type == RequestType.Announcement
            select new { req, PropertyName = prop.Name };

        var total = await query.LongCountAsync(ct);
        var rows = await query
            .OrderByDescending(x => x.req.CreatedAt)
            .ThenByDescending(x => x.req.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

        if (rows.Count == 0)
            return ([], total);

        var reqIds = rows.Select(r => r.req.Id).ToArray();
        var stats = await _db.RequestRecipients.AsNoTracking()
            .Where(r => reqIds.Contains(r.RequestId))
            .GroupBy(r => r.RequestId)
            .Select(g => new
            {
                RequestId = g.Key,
                Total = g.Count(),
                Read = g.Count(x => x.IsRead)
            })
            .ToDictionaryAsync(x => x.RequestId, ct);

        var items = rows.Select(r =>
        {
            stats.TryGetValue(r.req.Id, out var st);
            return new LandlordAnnouncementItemDto(
                r.req.Id,
                r.req.PropertyId,
                r.PropertyName,
                r.req.RecipientType,
                r.req.Title,
                r.req.Content,
                r.req.CreatedAt,
                st?.Total ?? 0,
                st?.Read ?? 0);
        }).ToList();

        return (items, total);
    }

    private async Task<Dictionary<(string UserId, Guid PropertyId), string>> GetUserRoomByPropertyAsync(
        IReadOnlyCollection<string> userIds, IReadOnlyCollection<Guid> propertyIds, CancellationToken ct)
    {
        if (userIds.Count == 0 || propertyIds.Count == 0)
            return [];

        var stays = await (
            from profile in _db.TenantProfiles.AsNoTracking()
            join link in _db.ContractTenants.AsNoTracking() on profile.Id equals link.TenantId
            join contract in _db.Contracts.AsNoTracking() on link.ContractId equals contract.Id
            join room in _db.Rooms.AsNoTracking() on contract.RoomId equals room.Id
            where profile.UserId != null &&
                  userIds.Contains(profile.UserId) &&
                  propertyIds.Contains(room.PropertyId) &&
                  contract.Status != ContractStatus.Cancelled
            orderby
                contract.Status == ContractStatus.Active ? 0 : 1,
                link.LeaveDate == null ? 0 : 1,
                contract.StartDate descending
            select new
            {
                UserId = profile.UserId!,
                room.PropertyId,
                room.RoomNumber
            }
        ).ToListAsync(ct);

        var result = new Dictionary<(string UserId, Guid PropertyId), string>();
        foreach (var stay in stays)
        {
            var key = (stay.UserId, stay.PropertyId);
            if (!result.ContainsKey(key))
            {
                result[key] = stay.RoomNumber;
            }
        }
        return result;
    }
}

