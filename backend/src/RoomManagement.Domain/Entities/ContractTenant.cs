using RoomManagement.Domain.Enums;

namespace RoomManagement.Domain.Entities;

public sealed class ContractTenant
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public Guid TenantId { get; set; }
    public DateOnly JoinDate { get; set; }
    public DateOnly? LeaveDate { get; set; }
    public bool IsContactPerson { get; set; }
    public bool OnlineRulesRequired { get; set; }
    public ContractTenantRole Role { get; set; } = ContractTenantRole.ContractParty;
    public DateTime? ResidenceApprovedAt { get; set; }
    public string? ResidenceApprovedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
