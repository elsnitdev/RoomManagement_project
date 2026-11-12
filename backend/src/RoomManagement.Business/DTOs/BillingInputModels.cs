using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Business.DTOs;

public sealed record CreateUtilityRateInput(UtilityType Type, decimal UnitPrice, string Unit,
    DateOnly EffectiveFrom, DateOnly? EffectiveTo);
public sealed record CreateMeterReadingInput(UtilityType Type, short Year, byte Month,
    decimal PreviousValue, decimal CurrentValue, string? Note);
public sealed record CreateServiceInput(string Name, string? Description, string? Unit,
    decimal? DefaultPrice);
public sealed record UpdateServiceInput(string Name, string? Description, string? Unit,
    decimal? DefaultPrice, bool IsActive);
public sealed record AssignRoomServiceInput(Guid ServiceId, decimal? CustomPrice,
    DateOnly StartDate, DateOnly? EndDate, int Quantity = 1);
public sealed record RoomServiceResult(RoomService Assignment, Service Service);
