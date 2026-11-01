namespace RoomManagement.Domain.Entities;

public sealed class RoomRow
{
    public Guid Id { get; set; }
    public Guid PropertyId { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public bool IsActive { get; set; } = true;
}
