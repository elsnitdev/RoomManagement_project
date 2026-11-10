namespace RoomManagement.Domain.Entities;

public sealed class ContractRulesEvidence
{
    public Guid Id { get; set; }
    public Guid AcceptanceId { get; set; }
    public string BlobName { get; set; } = "";
    public string OriginalFileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public string UploadedByUserId { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
