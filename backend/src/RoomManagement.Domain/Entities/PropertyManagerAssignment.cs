namespace RoomManagement.Domain.Entities;

/// <summary>
/// Phân công Quản lý dãy (Manager) phụ trách một Khu trọ (Property) của Chủ trọ.
/// </summary>
public sealed class PropertyManagerAssignment
{
    public Guid Id { get; set; }
    public string ManagerUserId { get; set; } = null!;
    public Guid PropertyId { get; set; }
    public string AssignedByUserId { get; set; } = null!;
    public DateTime AssignedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokedByUserId { get; set; }
}
