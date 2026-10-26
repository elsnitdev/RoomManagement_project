using RoomManagement.Domain.Enums;

namespace RoomManagement.Domain.Entities;

public sealed class ContractDocument
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public string FileName { get; set; } = null!;
    public string StorageKey { get; set; } = null!;
    public string? FileType { get; set; }
    public ContractDocumentType DocumentType { get; set; }
    public DateTime UploadedAt { get; set; }
}
