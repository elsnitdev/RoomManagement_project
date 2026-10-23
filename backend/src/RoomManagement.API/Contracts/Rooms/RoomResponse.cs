using RoomManagement.Domain.Enums;
namespace RoomManagement.API.Contracts.Rooms;

public sealed record RoomResponse(
    Guid Id,
    Guid PropertyId,
    string RoomNumber,
    decimal BasePrice,
    int MaxOccupants,
    RoomStatus Status);