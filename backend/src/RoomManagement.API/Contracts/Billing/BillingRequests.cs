using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using RoomManagement.Business.DTOs;
using RoomManagement.Domain.Enums;

namespace RoomManagement.API.Contracts.Billing;

public sealed record CreateUtilityRateRequest(
    [property: JsonRequired] UtilityType Type,
    [property: JsonRequired] decimal UnitPrice,
    [property: JsonRequired][Required, StringLength(50)] string Unit,
    [property: JsonRequired] DateOnly EffectiveFrom,
    DateOnly? EffectiveTo)
{
    public CreateUtilityRateInput ToInput() => new(Type, UnitPrice, Unit, EffectiveFrom, EffectiveTo);
}

public sealed record CreateMeterReadingRequest(
    [property: JsonRequired] UtilityType Type,
    [property: JsonRequired] short Year,
    [property: JsonRequired] byte Month,
    [property: JsonRequired] decimal PreviousValue,
    [property: JsonRequired] decimal CurrentValue,
    [StringLength(500)] string? Note)
{
    public CreateMeterReadingInput ToInput() => new(Type, Year, Month, PreviousValue, CurrentValue, Note);
}

public sealed record CreateServiceRequest(
    [property: JsonRequired][Required, StringLength(100)] string Name,
    [StringLength(500)] string? Description,
    [StringLength(50)] string? Unit,
    decimal? DefaultPrice)
{
    public CreateServiceInput ToInput() => new(Name, Description, Unit, DefaultPrice);
}

public sealed record UpdateServiceRequest(
    [property: JsonRequired][Required, StringLength(100)] string Name,
    [StringLength(500)] string? Description,
    [StringLength(50)] string? Unit,
    decimal? DefaultPrice,
    [property: JsonRequired] bool IsActive)
{
    public UpdateServiceInput ToInput() => new(Name, Description, Unit, DefaultPrice, IsActive);
}

public sealed record AssignRoomServiceRequest(
    [property: JsonRequired] Guid ServiceId,
    decimal? CustomPrice,
    [property: JsonRequired] DateOnly StartDate,
    DateOnly? EndDate,
    int Quantity = 1)
{
    public AssignRoomServiceInput ToInput() => new(ServiceId, CustomPrice, StartDate, EndDate, Quantity);
}
