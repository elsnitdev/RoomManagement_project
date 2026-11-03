using RoomManagement.Domain.Enums;
using RoomManagement.Domain.Entities;
namespace RoomManagement.Business.Interfaces;

public interface IContractStore
{
  Task<bool> RoomOwnedAsync(Guid propertyId, Guid roomId, string ownerId, CancellationToken ct);
  Task<Contract?> GetOwnedAsync(Guid propertyId, Guid roomId, string ownerId, CancellationToken ct);
  Task<(IReadOnlyList<Contract> Items, long Total)> ListOwnedAsync(
         Guid propertyId, string ownerId, Guid? roomId, ContractStatus? status,
         int skip, int take, CancellationToken ct);
  Task<bool> TenantProfileOwnedAsync(Guid tenantId, string ownerId, CancellationToken ct);
  Task<bool> HasCurrentTenantAsync(Guid contractId, CancellationToken ct);
  Task<bool> TenantAlreadyCurrentAsync(Guid contractId, Guid tenantId, CancellationToken ct);
  Task AddAsync(Contract contract, CancellationToken ct);
  Task AddTenantAsync(ContractTenant tenant, CancellationToken ct);
  Task SaveAsync(CancellationToken ct);

}
