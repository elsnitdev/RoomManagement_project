using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using RoomManagement.Business.DTOs;
using RoomManagement.Domain.Enums;

namespace RoomManagement.API.Contracts.Rooms;

public sealed record CreateRoomRequest(
    [property: JsonRequired, Required, StringLength(50, MinimumLength = 1)] string RoomNumber,
    [property: StringLength(100)] string? RoomName,
    int? Floor,
    decimal? Area,
    [property: JsonRequired] decimal BasePrice,
    [property: JsonRequired] int MaxOccupants,
    [property: StringLength(1000)] string? Description) : IValidatableObject
{
    public CreateRoomInput ToInput() => new(RoomNumber, RoomName, Floor, Area, BasePrice, MaxOccupants, Description);
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(RoomNumber)) yield return new ValidationResult("Mã phòng không được rỗng.", [nameof(RoomNumber)]);
        if (Area is <= 0) yield return new ValidationResult("Diện tích phải lớn hơn 0.", [nameof(Area)]);
        if (BasePrice < 0) yield return new ValidationResult("Giá phòng không được âm.", [nameof(BasePrice)]);
        if (MaxOccupants < 1) yield return new ValidationResult("Số người tối đa phải lớn hơn 0.", [nameof(MaxOccupants)]);
    }
}

public sealed record UpdateRoomRequest(
    [property: JsonRequired, Required, StringLength(50, MinimumLength = 1)] string RoomNumber,
    [property: StringLength(100)] string? RoomName,
    int? Floor,
    decimal? Area,
    [property: JsonRequired] decimal BasePrice,
    [property: JsonRequired] int MaxOccupants,
    [property: StringLength(1000)] string? Description,
    [property: JsonRequired] RoomStatus Status) : IValidatableObject
{
    public UpdateRoomInput ToInput() => new(RoomNumber, RoomName, Floor, Area, BasePrice, MaxOccupants, Description, Status);
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(RoomNumber)) yield return new ValidationResult("Mã phòng không được rỗng.", [nameof(RoomNumber)]);
        if (Area is <= 0) yield return new ValidationResult("Diện tích phải lớn hơn 0.", [nameof(Area)]);
        if (BasePrice < 0) yield return new ValidationResult("Giá phòng không được âm.", [nameof(BasePrice)]);
        if (MaxOccupants < 1) yield return new ValidationResult("Số người tối đa phải lớn hơn 0.", [nameof(MaxOccupants)]);
        if (Status is not (RoomStatus.Available or RoomStatus.Maintenance or RoomStatus.Inactive))
            yield return new ValidationResult("Chỉ được chọn Available, Maintenance hoặc Inactive.", [nameof(Status)]);
    }
}
