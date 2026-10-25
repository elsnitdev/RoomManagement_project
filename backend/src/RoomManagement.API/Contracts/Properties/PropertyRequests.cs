using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using RoomManagement.Business.DTOs;

namespace RoomManagement.API.Contracts.Properties;

public sealed record CreatePropertyRequest(
    [property: JsonRequired, Required, StringLength(150, MinimumLength = 1)] string Name,
    [property: JsonRequired, Required, StringLength(300, MinimumLength = 1)] string Address,
    [property: StringLength(1000)] string? Description) : IValidatableObject
{
    public CreatePropertyInput ToInput() => new(Name, Address, Description);
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Name)) yield return new ValidationResult("Tên khu trọ không được rỗng.", [nameof(Name)]);
        if (string.IsNullOrWhiteSpace(Address)) yield return new ValidationResult("Địa chỉ không được rỗng.", [nameof(Address)]);
    }
}

public sealed record UpdatePropertyRequest(
    [property: JsonRequired, Required, StringLength(150, MinimumLength = 1)] string Name,
    [property: JsonRequired, Required, StringLength(300, MinimumLength = 1)] string Address,
    [property: StringLength(1000)] string? Description,
    [property: JsonRequired] bool IsActive) : IValidatableObject
{
    public UpdatePropertyInput ToInput() => new(Name, Address, Description, IsActive);
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Name)) yield return new ValidationResult("Tên khu trọ không được rỗng.", [nameof(Name)]);
        if (string.IsNullOrWhiteSpace(Address)) yield return new ValidationResult("Địa chỉ không được rỗng.", [nameof(Address)]);
    }
}
