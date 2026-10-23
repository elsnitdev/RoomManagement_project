using RoomManagement.Domain.Enums;

namespace RoomManagement.Domain.Entities;

public sealed class MeterReading
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public UtilityType MeterType { get; set; }
    public short Year { get; set; }
    public byte Month { get; set; }
    public decimal PreviousValue { get; set; }
    public decimal CurrentValue { get; set; }
    public DateTime RecordedAt { get; set; }
    public string? Note { get; set; }
}
