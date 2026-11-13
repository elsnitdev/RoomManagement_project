using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RoomManagement.Business.DTOs.Managers;
using RoomManagement.Business.Errors;
using RoomManagement.Business.Interfaces;
using RoomManagement.Domain;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Data.Stores;

public sealed class ManagerStore : IManagerStore
{
    private readonly RoomManagementDbContext _db;

    public ManagerStore(RoomManagementDbContext db)
    {
        _db = db;
    }

    public Task<bool> IsUserNameTakenAsync(string normalizedUserName, CancellationToken ct = default) =>
        _db.Users.AnyAsync(u => u.NormalizedUserName == normalizedUserName, ct);

    public Task<bool> IsEmailTakenAsync(string normalizedEmail, CancellationToken ct = default) =>
        _db.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail, ct);

    public async Task<IReadOnlyList<Property>> GetOwnedPropertiesAsync(string landlordId, IReadOnlyCollection<Guid> propertyIds, CancellationToken ct = default) =>
        await _db.Properties.AsNoTracking()
            .Where(p => p.OwnerUserId == landlordId && propertyIds.Contains(p.Id))
            .ToListAsync(ct);

    public Task<AppUser?> FindManagerAsync(string landlordId, string managerId, bool asNoTracking = false, CancellationToken ct = default)
    {
        var query = asNoTracking ? _db.Users.AsNoTracking() : _db.Users.AsQueryable();
        return query.FirstOrDefaultAsync(
            u => u.Id == managerId
                && u.Role == UserRoles.Manager
                && u.LandlordUserId == landlordId
                && u.DeletedAt == null,
            ct);
    }

    public async Task<(IReadOnlyList<ManagerDto> Items, long TotalItems)> ListManagersAsync(
        string landlordId, string? search, bool? isActive, int skip, int take, CancellationToken ct = default)
    {
        var query = _db.Users.AsNoTracking()
            .Where(u => u.Role == UserRoles.Manager
                && u.LandlordUserId == landlordId
                && u.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(u =>
                u.UserName.Contains(search) ||
                (u.FullName != null && u.FullName.Contains(search)) ||
                (u.Email != null && u.Email.Contains(search)) ||
                (u.PhoneNumber != null && u.PhoneNumber.Contains(search)));
        }

        if (isActive.HasValue)
            query = query.Where(u => u.IsActive == isActive.Value);

        var total = await query.LongCountAsync(ct);
        var managers = await query
            .OrderByDescending(u => u.CreatedAt).ThenBy(u => u.Id)
            .Skip(skip).Take(take)
            .Select(u => new
            {
                u.Id,
                u.UserName,
                u.Email,
                u.FullName,
                u.PhoneNumber,
                u.IsActive,
                u.MustChangePassword,
                u.CreatedAt
            })
            .ToListAsync(ct);

        if (managers.Count == 0)
            return (Array.Empty<ManagerDto>(), total);

        var managerIds = managers.Select(m => m.Id).ToList();
        var assignments = await (
            from a in _db.PropertyManagerAssignments.AsNoTracking()
            join p in _db.Properties.AsNoTracking() on a.PropertyId equals p.Id
            where managerIds.Contains(a.ManagerUserId) && a.RevokedAt == null
            orderby p.Name, a.AssignedAt
            select new
            {
                a.ManagerUserId,
                a.PropertyId,
                p.Name,
                a.AssignedAt
            })
            .ToListAsync(ct);

        var lookup = assignments
            .GroupBy(x => x.ManagerUserId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<ManagerAssignedPropertyDto>)g
                    .Select(x => new ManagerAssignedPropertyDto(x.PropertyId, x.Name, x.AssignedAt))
                    .ToList());

        var items = managers.Select(m => new ManagerDto(
            m.Id,
            m.UserName,
            m.Email,
            m.FullName,
            m.PhoneNumber,
            m.IsActive,
            m.MustChangePassword,
            m.CreatedAt,
            lookup.GetValueOrDefault(m.Id, Array.Empty<ManagerAssignedPropertyDto>())))
            .ToList();

        return (items, total);
    }

    public async Task<ManagerDto?> GetManagerDetailsAsync(string landlordId, string managerId, CancellationToken ct = default)
    {
        var manager = await _db.Users.AsNoTracking()
            .Where(u => u.Id == managerId
                && u.Role == UserRoles.Manager
                && u.LandlordUserId == landlordId
                && u.DeletedAt == null)
            .Select(u => new
            {
                u.Id,
                u.UserName,
                u.Email,
                u.FullName,
                u.PhoneNumber,
                u.IsActive,
                u.MustChangePassword,
                u.CreatedAt
            })
            .FirstOrDefaultAsync(ct);

        if (manager is null)
            return null;

        var assignments = await (
            from a in _db.PropertyManagerAssignments.AsNoTracking()
            join p in _db.Properties.AsNoTracking() on a.PropertyId equals p.Id
            where a.ManagerUserId == manager.Id && a.RevokedAt == null
            orderby p.Name, a.AssignedAt
            select new ManagerAssignedPropertyDto(a.PropertyId, p.Name, a.AssignedAt))
            .ToListAsync(ct);

        return new ManagerDto(
            manager.Id,
            manager.UserName,
            manager.Email,
            manager.FullName,
            manager.PhoneNumber,
            manager.IsActive,
            manager.MustChangePassword,
            manager.CreatedAt,
            assignments);
    }

    public async Task CreateManagerAsync(AppUser manager, IReadOnlyList<PropertyManagerAssignment> initialAssignments, CancellationToken ct = default)
    {
        _db.Users.Add(manager);
        if (initialAssignments.Count > 0)
            _db.PropertyManagerAssignments.AddRange(initialAssignments);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new BusinessException(BusinessErrorType.Conflict, "MANAGER_CONFLICT", "Tên đăng nhập, email hoặc phân công khu trọ đã tồn tại.", ex);
        }
    }

    public async Task<IReadOnlyList<Guid>> GetActiveAssignedPropertyIdsAsync(string managerId, CancellationToken ct = default) =>
        await _db.PropertyManagerAssignments.AsNoTracking()
            .Where(a => a.ManagerUserId == managerId && a.RevokedAt == null)
            .Select(a => a.PropertyId)
            .ToListAsync(ct);

    public async Task AddAssignmentsAsync(IReadOnlyList<PropertyManagerAssignment> assignments, CancellationToken ct = default)
    {
        _db.PropertyManagerAssignments.AddRange(assignments);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new BusinessException(BusinessErrorType.Conflict, "ASSIGNMENT_ALREADY_EXISTS", "Quản lý đã được phân công khu trọ này.", ex);
        }
    }

    public async Task<bool> RevokeAssignmentAsync(string landlordId, string managerId, Guid propertyId, DateTime revokedAt, CancellationToken ct = default)
    {
        var managerExists = await _db.Users.AsNoTracking().AnyAsync(
            u => u.Id == managerId && u.Role == UserRoles.Manager && u.LandlordUserId == landlordId && u.DeletedAt == null,
            ct);
        if (!managerExists)
            return false;

        var assignment = await _db.PropertyManagerAssignments
            .FirstOrDefaultAsync(a => a.ManagerUserId == managerId && a.PropertyId == propertyId && a.RevokedAt == null, ct);
        if (assignment is null)
            return false;

        assignment.RevokedAt = revokedAt;
        assignment.RevokedByUserId = landlordId;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    public async Task<bool> DeleteOrDeactivateManagerAsync(string landlordId, string managerId, DateTime now, CancellationToken ct = default)
    {
        var manager = await _db.Users.FirstOrDefaultAsync(
            u => u.Id == managerId && u.Role == UserRoles.Manager && u.LandlordUserId == landlordId && u.DeletedAt == null,
            ct);
        if (manager is null)
            return false;

        var activeAssignments = await _db.PropertyManagerAssignments
            .Where(a => a.ManagerUserId == managerId && a.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var assignment in activeAssignments)
        {
            assignment.RevokedAt = now;
            assignment.RevokedByUserId = landlordId;
        }

        var hasReferences =
            await _db.PropertyManagerAssignments.AnyAsync(a => a.ManagerUserId == managerId, ct) ||
            await _db.TenantProfiles.AnyAsync(t => t.CreatedByUserId == managerId, ct) ||
            await _db.InvoiceAdjustments.AnyAsync(a => a.CreatedByUserId == managerId, ct) ||
            await _db.Payments.AnyAsync(p => p.SubmittedByUserId == managerId || p.VerifiedByUserId == managerId, ct) ||
            await _db.PaymentReviewEvents.AnyAsync(e => e.ReviewedByUserId == managerId, ct) ||
            await _db.Refunds.AnyAsync(r => r.CreatedByUserId == managerId, ct) ||
            await _db.ContractRulesVersions.AnyAsync(v => v.CreatedByUserId == managerId, ct) ||
            await _db.TenantRequests.AnyAsync(tr => tr.AssignedToUserId == managerId, ct);

        if (hasReferences)
        {
            manager.IsActive = false;
            manager.DeletedAt = now;
        }
        else
        {
            _db.Users.Remove(manager);
        }

        await _db.SaveChangesAsync(ct);
        return true;
    }
}
