namespace RoomManagement.Domain.Entities;

public sealed class RoomImage
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public string BlobName { get; set; } = null!;
    public string OriginalFileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public long SizeBytes { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
}
