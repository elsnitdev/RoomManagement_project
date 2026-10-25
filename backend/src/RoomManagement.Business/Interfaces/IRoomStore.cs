using RoomManagement.Business.DTOs;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Business.Interfaces;

public interface IRoomStore
{
    Task AddAsync(Room room, CancellationToken cancellationToken);
    Task<Room?> GetAsync(Guid propertyId, Guid roomId, string ownerId, CancellationToken cancellationToken);
    Task<RoomResult?> GetResultAsync(Guid propertyId, Guid roomId, string ownerId, CancellationToken cancellationToken);
    Task<(IReadOnlyList<RoomResult> Items, long TotalItems)> ListAsync(Guid propertyId, string ownerId, string? search, RoomStatus? status, int skip, int take, CancellationToken cancellationToken);
    Task<bool> NumberExistsAsync(Guid propertyId, string number, Guid? exceptRoomId, CancellationToken cancellationToken);
    Task<bool> HasActiveContractAsync(Guid roomId, CancellationToken cancellationToken);
    Task<string?> GetDeleteBlockerAsync(Guid roomId, CancellationToken cancellationToken);
    Task SaveAsync(CancellationToken cancellationToken);
    Task DeleteAsync(Room room, CancellationToken cancellationToken);
}
