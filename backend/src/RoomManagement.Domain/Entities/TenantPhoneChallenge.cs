namespace RoomManagement.Domain.Entities;

public sealed class TenantPhoneChallenge
{
    public Guid Id { get; set; }
    public string LandlordId { get; set; } = null!;
    public string PhoneNumber { get; set; } = null!;
    public string CodeHash { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int Attempts { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
