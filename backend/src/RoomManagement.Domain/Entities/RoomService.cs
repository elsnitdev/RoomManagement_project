namespace RoomManagement.Domain.Entities;

public sealed class RoomService
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public Guid ServiceId { get; set; }
    public decimal? CustomPrice { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public DateTime CreatedAt { get; set; }
}
