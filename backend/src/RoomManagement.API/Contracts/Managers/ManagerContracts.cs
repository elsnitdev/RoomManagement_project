using System.ComponentModel.DataAnnotations;
using RoomManagement.Business.DTOs.Managers;

namespace RoomManagement.API.Contracts.Managers;

public sealed record CreateManagerRequest(
    [Required, StringLength(256, MinimumLength = 1)] string UserName,
    [Required, MinLength(6)] string Password,
    [StringLength(256)] string? Email,
    [StringLength(150)] string? FullName,
    [StringLength(20)] string? PhoneNumber,
    IReadOnlyList<Guid>? PropertyIds)
{
    public CreateManagerInput ToInput() => new(UserName, Password, Email, FullName, PhoneNumber, PropertyIds);
}

public sealed record AssignManagerPropertiesRequest(
    [Required, MinLength(1)] IReadOnlyList<Guid> PropertyIds);

public sealed record ManagerAssignedPropertyResponse(Guid PropertyId, string PropertyName, DateTime AssignedAt)
{
    public static ManagerAssignedPropertyResponse From(ManagerAssignedPropertyDto dto) =>
        new(dto.PropertyId, dto.PropertyName, dto.AssignedAt);
}

public sealed record ManagerResponse(
    string Id,
    string UserName,
    string? Email,
    string? FullName,
    string? PhoneNumber,
    bool IsActive,
    bool MustChangePassword,
    DateTime CreatedAt,
    IReadOnlyList<ManagerAssignedPropertyResponse> AssignedProperties)
{
    public static ManagerResponse From(ManagerDto dto) => new(
        dto.Id,
        dto.UserName,
        dto.Email,
        dto.FullName,
        dto.PhoneNumber,
        dto.IsActive,
        dto.MustChangePassword,
        dto.CreatedAt,
        dto.AssignedProperties.Select(ManagerAssignedPropertyResponse.From).ToList());
}
