using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Business.DTOs;

public sealed record PageResult<T>(IReadOnlyList<T> Items, long TotalItems, int Page, int PageSize);
public sealed record PropertyFilter(int Page, int PageSize, string? Search, bool? IsActive);
public sealed record UpdatePropertyInput(
    string Name,
    string City,
    string Ward,
    string StreetAddress,
    string? Description,
    bool IsActive,
    string RowVersion)
{
    public UpdatePropertyInput(string name, string address, string? description, bool isActive, string rowVersion)
        : this(name, string.Empty, string.Empty, address, description, isActive, rowVersion)
    {
    }
}
public sealed record CreateRoomInput(string RoomNumber, string? RoomName, int? Floor, decimal? Area, decimal BasePrice, int MaxOccupants, string? Description);
public sealed record UpdateRoomInput(string RoomNumber, string? RoomName, int? Floor, decimal? Area, decimal BasePrice, int MaxOccupants, string? Description, RoomStatus Status, string RowVersion);
public sealed record RoomFilter(int Page, int PageSize, string? Search, RoomStatus? Status, Guid? RowId = null, int? Floor = null);
public sealed record RoomOccupant(Guid TenantId, string FullName, DateOnly JoinDate);
public enum RoomOccupancyState { Vacant, ContractedAwaitingMoveIn, Occupied, NeedsContract }
public sealed record RoomResult(Room Room, RoomStatus Status, Guid? ContractId = null,
    IReadOnlyList<RoomOccupant>? Occupants = null,
    RoomOccupancyState OccupancyState = RoomOccupancyState.Vacant);
