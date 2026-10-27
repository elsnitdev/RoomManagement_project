using RoomManagement.Domain.Enums;
using RoomManagement.Business.DTOs;
namespace RoomManagement.API.Contracts.Rooms;

public sealed record RoomResponse(
    Guid Id,
    Guid PropertyId,
    string RoomNumber,
    string? RoomName,
    int? Floor,
    decimal? Area,
    decimal BasePrice,
    int MaxOccupants,
    string? Description,
    RoomStatus Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string RowVersion)
{
    public static RoomResponse From(RoomResult result)
    {
        var room = result.Room;
        return new(room.Id, room.PropertyId, room.RoomNumber, room.RoomName, room.Floor, room.Area,
            room.BasePrice, room.MaxOccupants, room.Description, result.Status, room.CreatedAt, room.UpdatedAt, Convert.ToBase64String(room.RowVersion));
    }
}
