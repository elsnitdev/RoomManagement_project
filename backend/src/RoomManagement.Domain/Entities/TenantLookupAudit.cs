namespace RoomManagement.Domain.Entities;

/// <summary>
/// Nhật ký kiểm toán mỗi lần Landlord/Manager tra cứu chính xác tài khoản Tenant bằng SĐT + CCCD.
/// Chỉ lưu giá trị đã che (masked) để tránh lộ CCCD đầy đủ trong log/audit.
/// </summary>
public sealed class TenantLookupAudit
{
    public Guid Id { get; set; }
    public string ActorUserId { get; set; } = null!;
    public string OwnerLandlordUserId { get; set; } = null!;
    public Guid? PropertyId { get; set; }
    public string? MatchedUserId { get; set; }
    public bool IsMatched { get; set; }
    public string MaskedPhone { get; set; } = null!;
    public string MaskedCitizenId { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}
