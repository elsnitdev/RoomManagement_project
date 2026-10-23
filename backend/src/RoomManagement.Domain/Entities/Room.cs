using RoomManagement.Domain.Enums;

namespace RoomManagement.Domain.Entities;

public sealed class Room
{
    public Guid Id { get; set; }
    public Guid PropertyId { get; set; }
    public string RoomNumber { get; set; } = null!;
    public string? RoomName { get; set; }
    public int? Floor { get; set; }
    public decimal? Area { get; set; }
    public decimal BasePrice { get; set; }
    public int MaxOccupants { get; set; }
    public RoomStatus Status { get; set; } = RoomStatus.Available;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
