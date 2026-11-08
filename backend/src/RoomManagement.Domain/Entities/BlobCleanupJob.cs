namespace RoomManagement.Domain.Entities;

public sealed class BlobCleanupJob
{
    public Guid Id { get; set; }
    public string BlobName { get; set; } = null!;
    public int Attempts { get; set; }
    public DateTime NextAttemptAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
