using RoomManagement.Business.DTOs;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.API.Contracts.Billing;

public sealed record UtilityRateResponse(Guid Id, Guid PropertyId, UtilityType Type,
    decimal UnitPrice, string Unit, DateOnly EffectiveFrom, DateOnly? EffectiveTo)
{
    public static UtilityRateResponse From(UtilityRate x) =>
        new(x.Id, x.PropertyId, x.UtilityType, x.UnitPrice, x.Unit, x.EffectiveFrom, x.EffectiveTo);
}

public sealed record MeterReadingResponse(Guid Id, Guid RoomId, UtilityType Type,
    short Year, byte Month, decimal PreviousValue, decimal CurrentValue, decimal Used)
{
    public static MeterReadingResponse From(MeterReading x) =>
        new(x.Id, x.RoomId, x.MeterType, x.Year, x.Month,
            x.PreviousValue, x.CurrentValue, x.CurrentValue - x.PreviousValue);
}

public sealed record ServiceResponse(Guid Id, Guid? PropertyId, string Name, string? Description,
    string? Unit, decimal? DefaultPrice, bool IsActive)
{
    public static ServiceResponse From(Service x) =>
        new(x.Id, x.PropertyId, x.Name, x.Description, x.Unit, x.DefaultPrice, x.IsActive);
}

public sealed record RoomServiceResponse(Guid Id, Guid RoomId, Guid ServiceId,
    string ServiceName, decimal? UnitPrice, int Quantity, DateOnly StartDate, DateOnly? EndDate)
{
    public static RoomServiceResponse From(RoomServiceResult x) =>
        new(x.Assignment.Id, x.Assignment.RoomId, x.Assignment.ServiceId,
            x.Service.Name, x.Assignment.CustomPrice ?? x.Service.DefaultPrice, x.Assignment.Quantity,
            x.Assignment.StartDate, x.Assignment.EndDate);
}
