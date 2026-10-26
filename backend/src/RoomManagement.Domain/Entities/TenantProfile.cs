using RoomManagement.Domain.Enums;

namespace RoomManagement.Domain.Entities;

public sealed class TenantProfile
{
    public Guid Id { get; set; }
    public string? UserId { get; set; }
    public string FullName { get; set; } = null!;
    public string PhoneNumber { get; set; } = null!;
    public string? Email { get; set; }
    public string? CitizenId { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public Gender? Gender { get; set; }
    public string? PermanentAddress { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
