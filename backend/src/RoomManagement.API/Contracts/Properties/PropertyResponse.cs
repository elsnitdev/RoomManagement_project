using RoomManagement.Domain.Entities;

namespace RoomManagement.API.Contracts.Properties;

public sealed record PropertyResponse(Guid Id, string Name, string Address, string? Description, bool IsActive, DateTime CreatedAt, DateTime? UpdatedAt, string RowVersion)
{
  public static PropertyResponse From(Property property) =>
      new(property.Id, property.Name, property.Address, property.Description, property.IsActive, property.CreatedAt, property.UpdatedAt, Convert.ToBase64String(property.RowVersion));
}
