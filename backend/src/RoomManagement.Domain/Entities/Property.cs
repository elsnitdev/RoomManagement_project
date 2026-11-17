namespace RoomManagement.Domain.Entities;

public sealed class Property
{
    public Guid Id { get; set; }
    public string OwnerUserId { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string City { get; set; } = string.Empty;
    public string Ward { get; set; } = string.Empty;
    public string StreetAddress { get; set; } = string.Empty;
    public string Address { get; set; } = null!;
    public string? Description { get; set; }
    public string? BankName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankAccountHolder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
