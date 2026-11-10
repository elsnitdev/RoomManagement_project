namespace RoomManagement.Domain.Entities;

public sealed class ContractRulesVersion
{
    public Guid Id { get; set; }
    public Guid? ContractId { get; set; }
    public Guid? PropertyId { get; set; }
    public bool IsActive { get; set; }
    public int VersionNumber { get; set; }
    public string Content { get; set; } = null!;
    public string CreatedByUserId { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}
