using RoomManagement.Domain.Enums;
using RoomManagement.Business.DTOs;
namespace RoomManagement.API.Contracts.Rooms;

public sealed record RoomResponse(
    Guid Id,
    Guid PropertyId,
    Guid? RowId,
    int? SequenceNumber,
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
    string RowVersion,
    Guid? ContractId,
    IReadOnlyList<RoomOccupant> Occupants)
{
    public static RoomResponse From(RoomResult result)
    {
        var room = result.Room;
        return new(room.Id, room.PropertyId, room.RowId, room.SequenceNumber, room.RoomNumber, room.RoomName, room.Floor, room.Area,
            room.BasePrice, room.MaxOccupants, room.Description, result.Status, room.CreatedAt, room.UpdatedAt, Convert.ToBase64String(room.RowVersion),
            result.ContractId, result.Occupants ?? []);
    }
}
