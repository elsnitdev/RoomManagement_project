namespace RoomManagement.Domain.Entities;

public sealed class PaymentEvidence
{
    public Guid Id { get; set; }
    public Guid PaymentId { get; set; }
    public string BlobName { get; set; } = null!;
    public string OriginalFileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = null!;
    public string UploadedByUserId { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}
