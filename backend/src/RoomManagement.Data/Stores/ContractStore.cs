using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RoomManagement.Business.Errors;
using RoomManagement.Business.Interfaces;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Data;

public sealed class ContractStore : IContractStore
{
  private readonly RoomManagementDbContext _db;
  public ContractStore(RoomManagementDbContext db)
  {
    _db = db;
  }
  // các query thuộc contract thuộc landlord sẽ đi qua phần này 
  private IQueryable<Contract> Owned(Guid propertyId, string ownerId)
  {
    return
    from contract in _db.Contracts
    join room in _db.Rooms on contract.RoomId equals room.Id
    join property in _db.Properties on room.PropertyId equals property.Id
    where property.Id == propertyId && property.OwnerUserId == ownerId
    select contract;
  }

  public Task<bool> RoomOwnedAsync(Guid propertyId, Guid roomId, string ownerId, CancellationToken ct)
  {
    return
        _db.Rooms.AnyAsync(room => room.Id == roomId && room.PropertyId == propertyId &&
            _db.Properties.Any(property => property.Id == propertyId &&
                property.OwnerUserId == ownerId && property.IsActive), ct);
  }
  public Task<Contract?> GetOwnedAsync(Guid propertyId, Guid contractId, string ownerId, CancellationToken ct) =>
          Owned(propertyId, ownerId).SingleOrDefaultAsync(c => c.Id == contractId, ct);

  public async Task<(IReadOnlyList<Contract> Items, long Total)> ListOwnedAsync(
      Guid propertyId, string ownerId, Guid? roomId, ContractStatus? status,
      int skip, int take, CancellationToken ct)
  {
    var query = Owned(propertyId, ownerId).AsNoTracking();
    if (roomId.HasValue) query = query.Where(c => c.RoomId == roomId.Value);
    if (status.HasValue) query = query.Where(c => c.Status == status.Value);
    var total = await query.LongCountAsync(ct);
    var items = await query.OrderByDescending(c => c.CreatedAt).ThenBy(c => c.Id)
        .Skip(skip).Take(take).ToListAsync(ct);
    return (items, total);
  }
  public Task<bool> TenantProfileOwnedAsync(Guid tenantId, string ownerId, CancellationToken ct)
  {
    return _db.TenantProfiles.AnyAsync(t => t.Id == tenantId && t.CreatedByUserId == ownerId, ct);
  }
  public Task<bool> HasCurrentTenantAsync(Guid contractId, CancellationToken ct)
  {
    return _db.ContractTenants.AnyAsync(t => t.ContractId == contractId && t.LeaveDate == null, ct);
  }
  public Task<bool> TenantAlreadyCurrentAsync(Guid contractId, Guid tenantId, CancellationToken ct)
  {
    return _db.ContractTenants.AnyAsync(t => t.ContractId == contractId &&
            t.TenantId == tenantId && t.LeaveDate == null, ct);
  }
  public async Task AddAsync(Contract contract, CancellationToken ct)
  {
    _db.Contracts.Add(contract);
    await SaveAsync(ct);
  }

  public async Task AddTenantAsync(ContractTenant tenant, CancellationToken ct)
  {
    _db.ContractTenants.Add(tenant);
    await SaveAsync(ct);
  }

  public async Task SaveAsync(CancellationToken ct)
  {
    try { await _db.SaveChangesAsync(ct); }
    catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
    {
      throw new BusinessException(BusinessErrorType.Conflict,
          "CONTRACT_CONFLICT", "Mã hợp đồng, phòng đang thuê hoặc liên kết người thuê bị trùng.", ex);
    }
  }
}
