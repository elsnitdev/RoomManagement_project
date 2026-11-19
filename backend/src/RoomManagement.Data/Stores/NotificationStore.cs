using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RoomManagement.Business.DTOs;
using RoomManagement.Business.Interfaces;
using RoomManagement.Domain;
using RoomManagement.Domain.Constants;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Data.Stores;

public sealed class NotificationStore : INotificationStore
{
    private readonly RoomManagementDbContext _db;

    public NotificationStore(RoomManagementDbContext db)
    {
        _db = db;
    }

    public async Task<AdminNotificationTargetsDto> GetAdminTargetsAsync(
        string actorAdminId, string? search, CancellationToken ct = default)
    {
        var totalActiveSystemUsers = await _db.Users.AsNoTracking()
            .CountAsync(u =>
                u.Id != actorAdminId &&
                u.IsActive &&
                u.DeletedAt == null &&
                u.ApprovalStatus == ApprovalStatuses.Approved, ct);

        var landlordsQuery = _db.Users.AsNoTracking()
            .Where(u =>
                u.Role == UserRoles.Landlord &&
                u.IsActive &&
                u.DeletedAt == null &&
                u.ApprovalStatus == ApprovalStatuses.Approved);

        var totalActiveLandlords = await landlordsQuery.CountAsync(ct);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            landlordsQuery = landlordsQuery.Where(u =>
                (u.FullName != null && u.FullName.Contains(term)) ||
                u.UserName.Contains(term) ||
                (u.Email != null && u.Email.Contains(term)) ||
                (u.PhoneNumber != null && u.PhoneNumber.Contains(term)) ||
                (u.LandlordCode != null && u.LandlordCode.Contains(term)));
        }

        var landlords = await landlordsQuery
            .OrderBy(u => u.FullName ?? u.UserName)
            .Select(u => new AdminLandlordOptionDto(
                u.Id,
                u.FullName != null && u.FullName != string.Empty ? u.FullName : u.UserName,
                u.UserName,
                u.Email,
                u.PhoneNumber,
                u.LandlordCode,
                _db.Properties.Count(p => p.OwnerUserId == u.Id && p.IsActive)))
            .ToListAsync(ct);

        return new AdminNotificationTargetsDto(totalActiveSystemUsers, totalActiveLandlords, landlords);
    }

    public async Task<IReadOnlyList<string>> GetActiveSystemUserIdsAsync(
        string excludeUserId, CancellationToken ct = default)
    {
        return await _db.Users.AsNoTracking()
            .Where(u =>
                u.Id != excludeUserId &&
                u.IsActive &&
                u.DeletedAt == null &&
                u.ApprovalStatus == ApprovalStatuses.Approved)
            .Select(u => u.Id)
            .Distinct()
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<string>> GetActiveLandlordUserIdsAsync(CancellationToken ct = default)
    {
        return await _db.Users.AsNoTracking()
            .Where(u =>
                u.Role == UserRoles.Landlord &&
                u.IsActive &&
                u.DeletedAt == null &&
                u.ApprovalStatus == ApprovalStatuses.Approved)
            .Select(u => u.Id)
            .Distinct()
            .ToListAsync(ct);
    }

    public Task<bool> IsActiveApprovedLandlordAsync(string userId, CancellationToken ct = default)
    {
        return _db.Users.AsNoTracking().AnyAsync(u =>
            u.Id == userId &&
            u.Role == UserRoles.Landlord &&
            u.IsActive &&
            u.DeletedAt == null &&
            u.ApprovalStatus == ApprovalStatuses.Approved, ct);
    }

    public async Task<OperatorNotificationTargetsDto> GetOperatorTargetsAsync(
        OperatorContext context, string? search, CancellationToken ct = default)
    {
        var accessibleProps = await AccessibleActiveProperties(context)
            .OrderBy(p => p.Name)
            .Select(p => new { p.Id, p.Name, p.Address })
            .ToListAsync(ct);

        var propIds = accessibleProps.Select(p => p.Id).ToArray();
        if (propIds.Length == 0)
        {
            return new OperatorNotificationTargetsDto(0, [], []);
        }

        var rawStays = await (
            from contract in _db.Contracts.AsNoTracking()
            join room in _db.Rooms.AsNoTracking() on contract.RoomId equals room.Id
            join prop in _db.Properties.AsNoTracking() on room.PropertyId equals prop.Id
            join link in _db.ContractTenants.AsNoTracking() on contract.Id equals link.ContractId
            join profile in _db.TenantProfiles.AsNoTracking() on link.TenantId equals profile.Id
            join user in _db.Users.AsNoTracking() on profile.UserId equals user.Id
            where propIds.Contains(prop.Id) &&
                  contract.Status == ContractStatus.Active &&
                  link.LeaveDate == null &&
                  profile.UserId != null &&
                  user.IsActive &&
                  user.DeletedAt == null
            select new
            {
                UserId = user.Id,
                ProfileFullName = profile.FullName,
                UserFullName = user.FullName,
                user.UserName,
                ProfilePhone = profile.PhoneNumber,
                UserPhone = user.PhoneNumber,
                ProfileEmail = profile.Email,
                UserEmail = user.Email,
                PropertyId = prop.Id,
                PropertyName = prop.Name,
                RoomId = room.Id,
                room.RoomNumber,
                ContractId = contract.Id
            }
        ).ToListAsync(ct);

        var countByProperty = rawStays
            .GroupBy(x => x.PropertyId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.UserId).Distinct(StringComparer.Ordinal).Count());

        var propertyOptions = accessibleProps
            .Select(p => new OperatorPropertyOptionDto(
                p.Id,
                p.Name,
                p.Address,
                countByProperty.GetValueOrDefault(p.Id, 0)))
            .ToList();

        var allTenants = rawStays
            .GroupBy(x => x.UserId, StringComparer.Ordinal)
            .Select(g =>
            {
                var first = g.First();
                var fullName = FirstNonEmpty(first.ProfileFullName, first.UserFullName, first.UserName) ?? first.UserName;
                var phone = FirstNonEmpty(first.ProfilePhone, first.UserPhone);
                var email = FirstNonEmpty(first.ProfileEmail, first.UserEmail);
                var stays = g
                    .GroupBy(x => x.ContractId)
                    .Select(sg => sg.First())
                    .OrderBy(x => x.PropertyName, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(x => x.RoomNumber, StringComparer.CurrentCultureIgnoreCase)
                    .Select(x => new OperatorTenantStayDto(
                        x.PropertyId,
                        x.PropertyName,
                        x.RoomId,
                        x.RoomNumber,
                        x.ContractId))
                    .ToList();

                return new OperatorTenantOptionDto(g.Key, fullName, phone, email, stays);
            })
            .OrderBy(t => t.FullName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var totalActiveTenants = allTenants.Count;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            allTenants = allTenants
                .Where(t =>
                    t.FullName.Contains(term, StringComparison.CurrentCultureIgnoreCase) ||
                    (t.PhoneNumber != null && t.PhoneNumber.Contains(term, StringComparison.CurrentCultureIgnoreCase)) ||
                    (t.Email != null && t.Email.Contains(term, StringComparison.CurrentCultureIgnoreCase)) ||
                    t.ActiveStays.Any(s =>
                        s.RoomNumber.Contains(term, StringComparison.CurrentCultureIgnoreCase) ||
                        s.PropertyName.Contains(term, StringComparison.CurrentCultureIgnoreCase)))
                .ToList();
        }

        return new OperatorNotificationTargetsDto(totalActiveTenants, propertyOptions, allTenants);
    }

    public Task<bool> IsActivePropertyAccessibleAsync(
        OperatorContext context, Guid propertyId, CancellationToken ct = default)
    {
        return AccessibleActiveProperties(context).AnyAsync(p => p.Id == propertyId, ct);
    }

    public async Task<IReadOnlyList<string>> GetActiveTenantUserIdsForOperatorAsync(
        OperatorContext context, Guid? propertyId, CancellationToken ct = default)
    {
        var accessibleProps = AccessibleActiveProperties(context);
        if (propertyId.HasValue && propertyId.Value != Guid.Empty)
            accessibleProps = accessibleProps.Where(p => p.Id == propertyId.Value);

        return await (
            from contract in _db.Contracts.AsNoTracking()
            join room in _db.Rooms.AsNoTracking() on contract.RoomId equals room.Id
            join prop in accessibleProps on room.PropertyId equals prop.Id
            join link in _db.ContractTenants.AsNoTracking() on contract.Id equals link.ContractId
            join profile in _db.TenantProfiles.AsNoTracking() on link.TenantId equals profile.Id
            join user in _db.Users.AsNoTracking() on profile.UserId equals user.Id
            where contract.Status == ContractStatus.Active &&
                  link.LeaveDate == null &&
                  profile.UserId != null &&
                  user.IsActive &&
                  user.DeletedAt == null
            select user.Id
        ).Distinct().ToListAsync(ct);
    }

    public async Task<Guid?> ResolveActiveTenantPropertyIdForOperatorAsync(
        OperatorContext context, string tenantUserId, Guid? preferredPropertyId, CancellationToken ct = default)
    {
        var accessibleProps = AccessibleActiveProperties(context);
        if (preferredPropertyId.HasValue && preferredPropertyId.Value != Guid.Empty)
            accessibleProps = accessibleProps.Where(p => p.Id == preferredPropertyId.Value);

        return await (
            from contract in _db.Contracts.AsNoTracking()
            join room in _db.Rooms.AsNoTracking() on contract.RoomId equals room.Id
            join prop in accessibleProps on room.PropertyId equals prop.Id
            join link in _db.ContractTenants.AsNoTracking() on contract.Id equals link.ContractId
            join profile in _db.TenantProfiles.AsNoTracking() on link.TenantId equals profile.Id
            join user in _db.Users.AsNoTracking() on profile.UserId equals user.Id
            where user.Id == tenantUserId &&
                  contract.Status == ContractStatus.Active &&
                  link.LeaveDate == null &&
                  user.IsActive &&
                  user.DeletedAt == null
            orderby prop.Name, contract.StartDate descending
            select (Guid?)prop.Id
        ).FirstOrDefaultAsync(ct);
    }

    public Task<bool> EventKeyExistsAsync(string eventKey, CancellationToken ct = default)
    {
        return _db.UserNotifications.AsNoTracking().AnyAsync(n => n.EventKey == eventKey, ct);
    }

    public async Task<bool> AddNotificationsBatchAsync(
        IReadOnlyList<UserNotification> notifications, CancellationToken ct = default)
    {
        if (notifications.Count == 0)
            return true;

        _db.UserNotifications.AddRange(notifications);
        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            foreach (var entry in _db.ChangeTracker.Entries<UserNotification>().ToList())
                entry.State = EntityState.Detached;
            return false;
        }
    }

    public async Task<(IReadOnlyList<SentNotificationItemDto> Items, long Total)> ListSentNotificationsAsync(
        string actorUserId, int skip, int take, CancellationToken ct = default)
    {
        var groupedQuery = _db.UserNotifications.AsNoTracking()
            .Where(n => n.ActorUserId == actorUserId &&
                        n.EventKey != null &&
                        NotificationTypes.ManualSentTypes.Contains(n.Type))
            .GroupBy(n => n.EventKey!)
            .Select(g => new
            {
                EventKey = g.Key,
                CreatedAt = g.Max(x => x.CreatedAt),
                TotalRecipients = g.Count(),
                ReadCount = g.Count(x => x.IsRead)
            });

        var total = await groupedQuery.LongCountAsync(ct);
        if (total == 0)
            return ([], 0);

        var pageGroups = await groupedQuery
            .OrderByDescending(g => g.CreatedAt)
            .ThenByDescending(g => g.EventKey)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

        if (pageGroups.Count == 0)
            return ([], total);

        var eventKeys = pageGroups.Select(g => g.EventKey).ToArray();
        var samples = await _db.UserNotifications.AsNoTracking()
            .Where(n => n.ActorUserId == actorUserId &&
                        n.EventKey != null &&
                        eventKeys.Contains(n.EventKey))
            .GroupBy(n => n.EventKey!)
            .Select(g => g.OrderBy(x => x.UserId).First())
            .Select(x => new
            {
                x.EventKey,
                x.Type,
                x.Title,
                x.Message,
                x.PropertyId,
                x.UserId
            })
            .ToListAsync(ct);

        var sampleByKey = samples
            .Where(s => s.EventKey != null)
            .ToDictionary(s => s.EventKey!, StringComparer.Ordinal);

        var propertyIds = samples
            .Where(s => s.PropertyId.HasValue)
            .Select(s => s.PropertyId!.Value)
            .Distinct()
            .ToArray();

        var propertyMap = propertyIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await _db.Properties.AsNoTracking()
                .Where(p => propertyIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.Name, ct);

        var directUserIds = samples
            .Where(s => s.Type is NotificationTypes.AdminDirectLandlord or NotificationTypes.OperatorDirectTenant)
            .Select(s => s.UserId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var userMap = directUserIds.Length == 0
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : await _db.Users.AsNoTracking()
                .Where(u => directUserIds.Contains(u.Id))
                .ToDictionaryAsync(
                    u => u.Id,
                    u => u.FullName != null && u.FullName != string.Empty ? u.FullName : u.UserName,
                    StringComparer.Ordinal,
                    ct);

        var items = new List<SentNotificationItemDto>(pageGroups.Count);
        foreach (var grp in pageGroups)
        {
            if (!sampleByKey.TryGetValue(grp.EventKey, out var sample))
                continue;

            string? propertyName = sample.PropertyId.HasValue
                ? propertyMap.GetValueOrDefault(sample.PropertyId.Value)
                : null;
            string? directUserName = userMap.GetValueOrDefault(sample.UserId);

            var (targetScope, targetLabel) = ResolveTargetDisplay(sample.Type, propertyName, directUserName);
            items.Add(new SentNotificationItemDto(
                grp.EventKey,
                sample.Type,
                targetScope,
                targetLabel,
                sample.PropertyId,
                propertyName,
                sample.Title,
                sample.Message,
                grp.TotalRecipients,
                grp.ReadCount,
                grp.CreatedAt));
        }

        return (items, total);
    }

    private IQueryable<Property> AccessibleActiveProperties(OperatorContext context)
    {
        var query = _db.Properties.AsNoTracking()
            .Where(p => p.OwnerUserId == context.OwnerLandlordUserId && p.IsActive);

        if (context.IsManager)
        {
            query = query.Where(p => _db.PropertyManagerAssignments.Any(a =>
                a.PropertyId == p.Id &&
                a.ManagerUserId == context.ActorUserId &&
                a.RevokedAt == null));
        }

        return query;
    }

    private static (string TargetScope, string TargetLabel) ResolveTargetDisplay(
        string type, string? propertyName, string? directUserName) => type switch
    {
        NotificationTypes.SystemBroadcast => ("AllSystem", "Toàn bộ hệ thống"),
        NotificationTypes.AdminLandlordBroadcast => ("AllLandlords", "Tất cả chủ trọ"),
        NotificationTypes.AdminDirectLandlord => ("SpecificLandlord",
            !string.IsNullOrWhiteSpace(directUserName) ? $"Chủ trọ: {directUserName}" : "1 chủ trọ"),
        NotificationTypes.OperatorAllTenants => ("AllActiveTenants", "Tất cả người thuê HĐ hoạt động"),
        NotificationTypes.PropertyAnnouncement or NotificationTypes.ManagerAnnouncement or NotificationTypes.Announcement =>
            ("SpecificProperty", !string.IsNullOrWhiteSpace(propertyName) ? $"Khu trọ: {propertyName}" : "Theo khu trọ"),
        NotificationTypes.OperatorDirectTenant => ("SpecificTenant",
            !string.IsNullOrWhiteSpace(directUserName) ? $"Người thuê: {directUserName}" : "1 người thuê"),
        _ => ("General", "Thông báo")
    };

    private static string? FirstNonEmpty(params string?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate))
                return candidate.Trim();
        }
        return null;
    }
}
