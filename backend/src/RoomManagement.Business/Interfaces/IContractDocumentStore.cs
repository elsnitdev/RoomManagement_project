using RoomManagement.Domain.Entities;

namespace RoomManagement.Business.Interfaces;

public interface IContractDocumentStore
{
    Task<Contract?> GetOwnedAsync(Guid propertyId, Guid contractId, string ownerId, CancellationToken ct);
    Task<int?> TenantVisibleOfferVersionAsync(Guid contractId, string userId, CancellationToken ct);
    Task<IReadOnlyList<ContractDocument>> ListAsync(Guid contractId, CancellationToken ct);
    Task<ContractDocument?> GetAsync(Guid contractId, Guid documentId, CancellationToken ct);
    Task AddAsync(ContractDocument document, CancellationToken ct);
    Task DeleteDraftAsync(ContractDocument document, CancellationToken ct);
}
