using RoomManagement.Domain.Enums;

namespace RoomManagement.Domain.Entities;

public sealed class UtilityRate
{
    public Guid Id { get; set; }
    public Guid PropertyId { get; set; }
    public UtilityType UtilityType { get; set; }
    public decimal UnitPrice { get; set; }
    public string Unit { get; set; } = null!;
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public DateTime CreatedAt { get; set; }
}
