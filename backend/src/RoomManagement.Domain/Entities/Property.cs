namespace RoomManagement.Domain.Entities;

public sealed class Property
{
    public Guid Id { get; set; }
    public string OwnerUserId { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Address { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
