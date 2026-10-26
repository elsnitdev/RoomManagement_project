using RoomManagement.Domain.Entities;

namespace RoomManagement.Business.Interfaces;

public interface IPropertyStore
{
    Task AddAsync(Property property, CancellationToken cancellationToken);
    Task<Property?> GetAsync(Guid propertyId, string ownerId, CancellationToken cancellationToken);
    Task<(IReadOnlyList<Property> Items, long TotalItems)> ListAsync(string ownerId, string? search, bool? isActive, int skip, int take, CancellationToken cancellationToken);
    Task SaveAsync(CancellationToken cancellationToken);
    Task<string?> GetDeleteBlockerAsync(Guid propertyId, CancellationToken cancellationToken);
    Task DeleteAsync(Property property, CancellationToken cancellationToken);
}
