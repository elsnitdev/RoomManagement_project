namespace RoomManagement.Domain.Entities;

public sealed class RoomNumberCounter
{
    public Guid PropertyId { get; set; }
    public string ScopeKey { get; set; } = null!;
    public int LastSequence { get; set; }
}
