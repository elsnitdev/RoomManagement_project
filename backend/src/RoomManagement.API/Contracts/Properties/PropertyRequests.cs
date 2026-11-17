using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using RoomManagement.Business.DTOs;

namespace RoomManagement.API.Contracts.Properties;

public sealed record CreatePropertyRequest(
    [property: JsonRequired][Required, StringLength(150, MinimumLength = 1)] string Name,
    [property: JsonRequired][Required, StringLength(100, MinimumLength = 1)] string City,
    [property: JsonRequired][Required, StringLength(100, MinimumLength = 1)] string Ward,
    [property: JsonRequired][Required, StringLength(200, MinimumLength = 1)] string StreetAddress,
    [StringLength(1000)] string? Description) : IValidatableObject
{
    public CreatePropertyInput ToInput() => new(Name, City, Ward, StreetAddress, Description);
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Name)) yield return new ValidationResult("Tên khu trọ không được rỗng.", [nameof(Name)]);
        if (string.IsNullOrWhiteSpace(City)) yield return new ValidationResult("Tỉnh/Thành phố không được rỗng.", [nameof(City)]);
        if (string.IsNullOrWhiteSpace(Ward)) yield return new ValidationResult("Phường/Xã không được rỗng.", [nameof(Ward)]);
        if (string.IsNullOrWhiteSpace(StreetAddress)) yield return new ValidationResult("Địa chỉ cụ thể không được rỗng.", [nameof(StreetAddress)]);
    }
}

public sealed record UpdatePropertyRequest(
    [property: JsonRequired][Required, StringLength(150, MinimumLength = 1)] string Name,
    [property: JsonRequired][Required, StringLength(100, MinimumLength = 1)] string City,
    [property: JsonRequired][Required, StringLength(100, MinimumLength = 1)] string Ward,
    [property: JsonRequired][Required, StringLength(200, MinimumLength = 1)] string StreetAddress,
    [StringLength(1000)] string? Description,
    [property: JsonRequired] bool IsActive,
    [property: JsonRequired][Required] string RowVersion) : IValidatableObject
{
    public UpdatePropertyInput ToInput() => new(Name, City, Ward, StreetAddress, Description, IsActive, RowVersion);
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Name)) yield return new ValidationResult("Tên khu trọ không được rỗng.", [nameof(Name)]);
        if (string.IsNullOrWhiteSpace(City)) yield return new ValidationResult("Tỉnh/Thành phố không được rỗng.", [nameof(City)]);
        if (string.IsNullOrWhiteSpace(Ward)) yield return new ValidationResult("Phường/Xã không được rỗng.", [nameof(Ward)]);
        if (string.IsNullOrWhiteSpace(StreetAddress)) yield return new ValidationResult("Địa chỉ cụ thể không được rỗng.", [nameof(StreetAddress)]);
    }
}

public sealed record UpdatePaymentAccountRequest(
    [StringLength(150)] string? BankName,
    [StringLength(50)] string? AccountNumber,
    [StringLength(150)] string? AccountHolder,
    [property: JsonRequired][Required] string RowVersion);
