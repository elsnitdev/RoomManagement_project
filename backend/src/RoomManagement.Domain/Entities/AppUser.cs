namespace RoomManagement.Domain.Entities;

public sealed class AppUser
{
    public string Id { get; set; } = null!;
    public string UserName { get; set; } = null!;
    public string NormalizedUserName { get; set; } = null!;
    public string? Email { get; set; }
    public string? NormalizedEmail { get; set; }
    public string PasswordHash { get; set; } = null!;
    public string Role { get; set; } = null!;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
