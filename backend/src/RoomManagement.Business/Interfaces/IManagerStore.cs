using RoomManagement.Business.DTOs.Managers;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Business.Interfaces;

public interface IManagerStore
{
    Task<bool> IsUserNameTakenAsync(string normalizedUserName, CancellationToken ct = default);
    Task<bool> IsEmailTakenAsync(string normalizedEmail, CancellationToken ct = default);
    Task<IReadOnlyList<Property>> GetOwnedPropertiesAsync(string landlordId, IReadOnlyCollection<Guid> propertyIds, CancellationToken ct = default);
    Task<AppUser?> FindManagerAsync(string landlordId, string managerId, bool asNoTracking = false, CancellationToken ct = default);
    Task<(IReadOnlyList<ManagerDto> Items, long TotalItems)> ListManagersAsync(string landlordId, string? search, bool? isActive, int skip, int take, CancellationToken ct = default);
    Task<ManagerDto?> GetManagerDetailsAsync(string landlordId, string managerId, CancellationToken ct = default);
    Task CreateManagerAsync(AppUser manager, IReadOnlyList<PropertyManagerAssignment> initialAssignments, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> GetActiveAssignedPropertyIdsAsync(string managerId, CancellationToken ct = default);
    Task AddAssignmentsAsync(IReadOnlyList<PropertyManagerAssignment> assignments, CancellationToken ct = default);
    Task<bool> RevokeAssignmentAsync(string landlordId, string managerId, Guid propertyId, DateTime revokedAt, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
    Task<bool> DeleteOrDeactivateManagerAsync(string landlordId, string managerId, DateTime now, CancellationToken ct = default);
}
