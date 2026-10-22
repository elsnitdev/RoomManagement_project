namespace RoomManagement.Domain.Entities;

public sealed class ContractTenant
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public Guid TenantId { get; set; }
    public DateOnly JoinDate { get; set; }
    public DateOnly? LeaveDate { get; set; }
    public DateTime CreatedAt { get; set; }
}
