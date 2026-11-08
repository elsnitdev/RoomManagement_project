using RoomManagement.Domain.Entities;

namespace RoomManagement.Business.Interfaces;

public interface IRoomImageStore
{
    Task<bool> RoomOwnedAsync(Guid propertyId, Guid roomId, string ownerId, CancellationToken ct);
    Task<IReadOnlyList<RoomImage>> ListAsync(Guid roomId, CancellationToken ct);
    Task<RoomImage?> GetAsync(Guid roomId, Guid imageId, CancellationToken ct);
    Task AddAsync(RoomImage image, CancellationToken ct);
    Task DeleteAndQueueCleanupAsync(RoomImage image, CancellationToken ct);
    Task SetOrderAsync(Guid roomId, IReadOnlyList<Guid> ids, CancellationToken ct);
}
