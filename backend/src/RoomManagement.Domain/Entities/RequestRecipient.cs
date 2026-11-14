using RoomManagement.Domain.Enums;

namespace RoomManagement.Domain.Entities;

public sealed class RequestRecipient
{
    public Guid Id { get; set; }
    public Guid RequestId { get; set; }
    public string ReceiverUserId { get; set; } = null!;
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public RequestRecipientStatus Status { get; set; } = RequestRecipientStatus.Open;
}

