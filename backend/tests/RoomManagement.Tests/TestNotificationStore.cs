using System.Collections.Concurrent;
using RoomManagement.Business.DTOs;
using RoomManagement.Business.Interfaces;
using RoomManagement.Domain;
using RoomManagement.Domain.Constants;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Tests;

internal sealed record TestActiveStay(
    string TenantUserId,
    string FullName,
    string? PhoneNumber,
    string? Email,
    Guid PropertyId,
    string PropertyName,
    Guid RoomId,
    string RoomNumber,
    Guid ContractId);

internal sealed class TestNotificationStore : INotificationStore
{
    private readonly TestUserStore _userStore;

    public TestNotificationStore(TestUserStore userStore)
    {
        _userStore = userStore;
    }

    public ConcurrentDictionary<Guid, Property> Properties { get; } = new();
    public ConcurrentDictionary<string, HashSet<Guid>> ManagerAssignments { get; } = new(StringComparer.Ordinal);
    public List<TestActiveStay> ActiveStays { get; } = [];
    public List<UserNotification> Notifications { get; } = [];

    private IEnumerable<Property> Accessible(OperatorContext context)
    {
        var baseProps = Properties.Values.Where(p => p.OwnerUserId == context.OwnerLandlordUserId && p.IsActive);
        if (!context.IsManager) return baseProps;
        var assigned = ManagerAssignments.GetValueOrDefault(context.ActorUserId) ?? [];
        return baseProps.Where(p => assigned.Contains(p.Id));
    }

    public Task<AdminNotificationTargetsDto> GetAdminTargetsAsync(string actorAdminId, string? search, CancellationToken ct = default)
    {
        var activeSystemUsers = _userStore.Users.Values
            .Count(u => u.Id != actorAdminId && u.IsActive && u.DeletedAt == null && u.ApprovalStatus == ApprovalStatuses.Approved);

        var landlordsBase = _userStore.Users.Values
            .Where(u => u.Role == UserRoles.Landlord && u.IsActive && u.DeletedAt == null && u.ApprovalStatus == ApprovalStatuses.Approved)
            .ToList();

        var totalLandlords = landlordsBase.Count;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            landlordsBase = landlordsBase
                .Where(u =>
                    (u.FullName != null && u.FullName.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                    u.UserName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (u.Email != null && u.Email.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                    (u.PhoneNumber != null && u.PhoneNumber.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                    (u.LandlordCode != null && u.LandlordCode.Contains(term, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        var options = landlordsBase
            .OrderBy(u => u.FullName ?? u.UserName, StringComparer.OrdinalIgnoreCase)
            .Select(u => new AdminLandlordOptionDto(
                u.Id,
                !string.IsNullOrWhiteSpace(u.FullName) ? u.FullName! : u.UserName,
                u.UserName,
                u.Email,
                u.PhoneNumber,
                u.LandlordCode,
                Properties.Values.Count(p => p.OwnerUserId == u.Id && p.IsActive)))
            .ToList();

        return Task.FromResult(new AdminNotificationTargetsDto(activeSystemUsers, totalLandlords, options));
    }

    public Task<IReadOnlyList<string>> GetActiveSystemUserIdsAsync(string excludeUserId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<string>>(_userStore.Users.Values
            .Where(u => u.Id != excludeUserId && u.IsActive && u.DeletedAt == null && u.ApprovalStatus == ApprovalStatuses.Approved)
            .Select(u => u.Id)
            .Distinct(StringComparer.Ordinal)
            .ToList());

    public Task<IReadOnlyList<string>> GetActiveLandlordUserIdsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<string>>(_userStore.Users.Values
            .Where(u => u.Role == UserRoles.Landlord && u.IsActive && u.DeletedAt == null && u.ApprovalStatus == ApprovalStatuses.Approved)
            .Select(u => u.Id)
            .Distinct(StringComparer.Ordinal)
            .ToList());

    public Task<bool> IsActiveApprovedLandlordAsync(string userId, CancellationToken ct = default) =>
        Task.FromResult(_userStore.Users.Values.Any(u =>
            u.Id == userId && u.Role == UserRoles.Landlord && u.IsActive && u.DeletedAt == null && u.ApprovalStatus == ApprovalStatuses.Approved));

    public Task<OperatorNotificationTargetsDto> GetOperatorTargetsAsync(OperatorContext context, string? search, CancellationToken ct = default)
    {
        var props = Accessible(context).OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
        var propIds = props.Select(p => p.Id).ToHashSet();
        var validStays = ActiveStays
            .Where(s => propIds.Contains(s.PropertyId) &&
                        _userStore.Users.TryGetValue(s.TenantUserId, out var u) &&
                        u.IsActive && u.DeletedAt == null)
            .ToList();

        var propOptions = props
            .Select(p => new OperatorPropertyOptionDto(
                p.Id,
                p.Name,
                p.Address,
                validStays.Where(s => s.PropertyId == p.Id).Select(s => s.TenantUserId).Distinct(StringComparer.Ordinal).Count()))
            .ToList();

        var allTenants = validStays
            .GroupBy(s => s.TenantUserId, StringComparer.Ordinal)
            .Select(g =>
            {
                var first = g.First();
                return new OperatorTenantOptionDto(
                    g.Key,
                    first.FullName,
                    first.PhoneNumber,
                    first.Email,
                    g.Select(x => new OperatorTenantStayDto(x.PropertyId, x.PropertyName, x.RoomId, x.RoomNumber, x.ContractId)).ToList());
            })
            .OrderBy(t => t.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var totalTenants = allTenants.Count;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            allTenants = allTenants
                .Where(t =>
                    t.FullName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (t.PhoneNumber != null && t.PhoneNumber.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                    t.ActiveStays.Any(s =>
                        s.RoomNumber.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                        s.PropertyName.Contains(term, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        return Task.FromResult(new OperatorNotificationTargetsDto(totalTenants, propOptions, allTenants));
    }

    public Task<bool> IsActivePropertyAccessibleAsync(OperatorContext context, Guid propertyId, CancellationToken ct = default) =>
        Task.FromResult(Accessible(context).Any(p => p.Id == propertyId));

    public Task<IReadOnlyList<string>> GetActiveTenantUserIdsForOperatorAsync(OperatorContext context, Guid? propertyId, CancellationToken ct = default)
    {
        var propIds = Accessible(context)
            .Where(p => !propertyId.HasValue || p.Id == propertyId.Value)
            .Select(p => p.Id)
            .ToHashSet();

        return Task.FromResult<IReadOnlyList<string>>(ActiveStays
            .Where(s => propIds.Contains(s.PropertyId) &&
                        _userStore.Users.TryGetValue(s.TenantUserId, out var u) &&
                        u.IsActive && u.DeletedAt == null)
            .Select(s => s.TenantUserId)
            .Distinct(StringComparer.Ordinal)
            .ToList());
    }

    public Task<Guid?> ResolveActiveTenantPropertyIdForOperatorAsync(
        OperatorContext context, string tenantUserId, Guid? preferredPropertyId, CancellationToken ct = default)
    {
        var propIds = Accessible(context)
            .Where(p => !preferredPropertyId.HasValue || p.Id == preferredPropertyId.Value)
            .Select(p => p.Id)
            .ToHashSet();

        var match = ActiveStays.FirstOrDefault(s =>
            s.TenantUserId == tenantUserId &&
            propIds.Contains(s.PropertyId) &&
            _userStore.Users.TryGetValue(s.TenantUserId, out var u) &&
            u.IsActive && u.DeletedAt == null);

        return Task.FromResult<Guid?>(match?.PropertyId);
    }

    public Task<bool> EventKeyExistsAsync(string eventKey, CancellationToken ct = default) =>
        Task.FromResult(Notifications.Any(n => n.EventKey == eventKey));

    public Task<bool> AddNotificationsBatchAsync(IReadOnlyList<UserNotification> notifications, CancellationToken ct = default)
    {
        if (notifications.Any(n => Notifications.Any(x => x.UserId == n.UserId && x.EventKey == n.EventKey)))
            return Task.FromResult(false);

        Notifications.AddRange(notifications);
        return Task.FromResult(true);
    }

    public Task<(IReadOnlyList<SentNotificationItemDto> Items, long Total)> ListSentNotificationsAsync(
        string actorUserId, int skip, int take, CancellationToken ct = default)
    {
        var groups = Notifications
            .Where(n => n.ActorUserId == actorUserId && n.EventKey != null && NotificationTypes.ManualSentTypes.Contains(n.Type))
            .GroupBy(n => n.EventKey!, StringComparer.Ordinal)
            .Select(g =>
            {
                var first = g.OrderBy(x => x.UserId, StringComparer.Ordinal).First();
                var propName = first.PropertyId.HasValue && Properties.TryGetValue(first.PropertyId.Value, out var prop)
                    ? prop.Name
                    : null;
                string? directUserName = null;
                if (first.Type is NotificationTypes.AdminDirectLandlord or NotificationTypes.OperatorDirectTenant &&
                    _userStore.Users.TryGetValue(first.UserId, out var recipient))
                {
                    directUserName = !string.IsNullOrWhiteSpace(recipient.FullName) ? recipient.FullName : recipient.UserName;
                }

                var (scope, label) = first.Type switch
                {
                    NotificationTypes.SystemBroadcast => ("AllSystem", "Toàn bộ hệ thống"),
                    NotificationTypes.AdminLandlordBroadcast => ("AllLandlords", "Tất cả chủ trọ"),
                    NotificationTypes.AdminDirectLandlord => ("SpecificLandlord", $"Chủ trọ: {directUserName}"),
                    NotificationTypes.OperatorAllTenants => ("AllActiveTenants", "Tất cả người thuê HĐ hoạt động"),
                    NotificationTypes.PropertyAnnouncement or NotificationTypes.ManagerAnnouncement or NotificationTypes.Announcement =>
                        ("SpecificProperty", propName != null ? $"Khu trọ: {propName}" : "Theo khu trọ"),
                    NotificationTypes.OperatorDirectTenant => ("SpecificTenant", $"Người thuê: {directUserName}"),
                    _ => ("General", "Thông báo")
                };

                return new SentNotificationItemDto(
                    g.Key,
                    first.Type,
                    scope,
                    label,
                    first.PropertyId,
                    propName,
                    first.Title,
                    first.Message,
                    g.Count(),
                    g.Count(x => x.IsRead),
                    g.Max(x => x.CreatedAt));
            })
            .OrderByDescending(x => x.CreatedAt)
            .ToList();

        return Task.FromResult<(IReadOnlyList<SentNotificationItemDto>, long)>(
            (groups.Skip(skip).Take(take).ToList(), groups.Count));
    }
}
