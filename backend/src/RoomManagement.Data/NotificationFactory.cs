using Microsoft.EntityFrameworkCore;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Data;

internal static class NotificationFactory
{
    public static async Task AddForContractMembersAsync(RoomManagementDbContext db, Guid contractId,
        string type, string title, string message, string eventKey, Guid? invoiceId,
        string? actorUserId, CancellationToken ct)
    {
        var users = await (from link in db.ContractTenants.AsNoTracking()
                           join profile in db.TenantProfiles.AsNoTracking() on link.TenantId equals profile.Id
                           join user in db.Users.AsNoTracking() on profile.UserId equals user.Id
                           where link.ContractId == contractId && link.LeaveDate == null &&
                                 user.IsActive && user.DeletedAt == null
                           select user.Id).Distinct().ToListAsync(ct);
        var propertyId = await (from contract in db.Contracts.AsNoTracking()
                                join room in db.Rooms.AsNoTracking() on contract.RoomId equals room.Id
                                where contract.Id == contractId
                                select (Guid?)room.PropertyId).SingleOrDefaultAsync(ct);
        var now = DateTime.UtcNow;
        db.UserNotifications.AddRange(users.Select(id => new UserNotification
        {
            Id = Guid.NewGuid(), UserId = id, Type = type, Title = title, Message = message,
            ContractId = contractId, InvoiceId = invoiceId, PropertyId = propertyId,
            ActorUserId = actorUserId, EventKey = eventKey, CreatedAt = now
        }));
    }

    public static async Task AddForPropertyOperatorsAsync(RoomManagementDbContext db, Guid contractId,
        string type, string title, string message, string eventKey, Guid? invoiceId,
        string? actorUserId, CancellationToken ct)
    {
        var propertyInfo = await (from contract in db.Contracts.AsNoTracking()
                                  join room in db.Rooms.AsNoTracking() on contract.RoomId equals room.Id
                                  join property in db.Properties.AsNoTracking() on room.PropertyId equals property.Id
                                  where contract.Id == contractId
                                  select new { property.Id, property.OwnerUserId }).SingleOrDefaultAsync(ct);
        if (propertyInfo is null) return;

        var operatorIds = new HashSet<string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(propertyInfo.OwnerUserId))
        {
            var ownerActive = await db.Users.AsNoTracking()
                .AnyAsync(u => u.Id == propertyInfo.OwnerUserId && u.IsActive && u.DeletedAt == null, ct);
            if (ownerActive)
                operatorIds.Add(propertyInfo.OwnerUserId);
        }

        var managerIds = await (from assignment in db.PropertyManagerAssignments.AsNoTracking()
                                join manager in db.Users.AsNoTracking() on assignment.ManagerUserId equals manager.Id
                                where assignment.PropertyId == propertyInfo.Id && assignment.RevokedAt == null &&
                                      manager.IsActive && manager.DeletedAt == null
                                select manager.Id).Distinct().ToListAsync(ct);
        foreach (var managerId in managerIds)
            operatorIds.Add(managerId);

        if (operatorIds.Count == 0) return;

        var now = DateTime.UtcNow;
        db.UserNotifications.AddRange(operatorIds.Select(id => new UserNotification
        {
            Id = Guid.NewGuid(), UserId = id, Type = type, Title = title, Message = message,
            ContractId = contractId, InvoiceId = invoiceId, PropertyId = propertyInfo.Id,
            ActorUserId = actorUserId, EventKey = eventKey, CreatedAt = now
        }));
    }
}
