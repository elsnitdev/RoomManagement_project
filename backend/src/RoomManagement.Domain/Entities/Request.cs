using RoomManagement.Domain.Enums;

namespace RoomManagement.Domain.Entities;

public sealed class Request
{
    public Guid Id { get; set; }
    public Guid PropertyId { get; set; }
    public string SenderUserId { get; set; } = null!;
    public RequestRecipientType RecipientType { get; set; }
    public string Title { get; set; } = null!;
    public string Content { get; set; } = null!;
    public RequestType Type { get; set; }
    public DateTime CreatedAt { get; set; }
}

