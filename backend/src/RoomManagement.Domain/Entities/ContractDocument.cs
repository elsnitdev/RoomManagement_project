namespace RoomManagement.Domain.Entities;

public sealed class ContractDocument
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public string FileName { get; set; } = null!;
    public string StorageKey { get; set; } = null!;
    public string? FileType { get; set; }
    public string DocumentType { get; set; } = null!;
    public DateTime UploadedAt { get; set; }
}
