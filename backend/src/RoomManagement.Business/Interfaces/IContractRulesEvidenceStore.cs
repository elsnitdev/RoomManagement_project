using RoomManagement.Domain.Entities;

namespace RoomManagement.Business.Interfaces;

public sealed record RulesEvidenceItem(ContractRulesEvidence Evidence, int VersionNumber, bool IsLegacyContractRules);

public interface IContractRulesEvidenceStore
{
    Task<bool> TenantCanAccessAsync(Guid contractId, Guid linkId, string userId, CancellationToken ct);
    Task<bool> OwnerCanAccessAsync(Guid propertyId, Guid contractId, Guid linkId, string userId, CancellationToken ct);
    Task<Guid?> CurrentAcceptanceAsync(Guid contractId, Guid linkId, string userId, CancellationToken ct);
    Task<IReadOnlyList<RulesEvidenceItem>> ListAsync(Guid contractId, Guid linkId, CancellationToken ct);
    Task AddAsync(ContractRulesEvidence evidence, CancellationToken ct);
}
