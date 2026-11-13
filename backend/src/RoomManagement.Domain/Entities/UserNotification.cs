namespace RoomManagement.Domain.Entities;

/// <summary>
/// Thông báo trong ứng dụng gửi tới người dùng (ví dụ khi có đề nghị hợp đồng hoặc kết thúc hợp đồng/rời phòng).
/// </summary>
public sealed class UserNotification
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Message { get; set; } = null!;
    public Guid? ContractId { get; set; }
    public Guid? PropertyId { get; set; }
    public string? ActorUserId { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
