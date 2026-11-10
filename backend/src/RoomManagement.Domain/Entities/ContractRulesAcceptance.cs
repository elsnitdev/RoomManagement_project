namespace RoomManagement.Domain.Entities;

public sealed class ContractRulesAcceptance
{
    public Guid Id { get; set; }
    public Guid RulesVersionId { get; set; }
    public Guid ContractTenantId { get; set; }
    public string AcceptedByUserId { get; set; } = null!;
    public DateTime AcceptedAt { get; set; }
}
