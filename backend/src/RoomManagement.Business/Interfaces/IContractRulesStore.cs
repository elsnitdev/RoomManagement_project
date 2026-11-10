using RoomManagement.Business.DTOs;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Business.Interfaces;

public interface IContractRulesStore
{
    Task<ContractRulesVersion?> GetOwnedAsync(Guid propertyId, string ownerId, CancellationToken ct);
    Task<ContractRulesVersion> SaveAsync(Guid propertyId, string ownerId, string content, Guid? expectedVersionId, CancellationToken ct);
    Task<IReadOnlyList<ContractRulesTenantView>> ListMineAsync(string userId, CancellationToken ct);
    Task<ContractRulesAcceptance> AcceptAsync(Guid contractId, Guid linkId, Guid versionId, string userId, CancellationToken ct);
}
