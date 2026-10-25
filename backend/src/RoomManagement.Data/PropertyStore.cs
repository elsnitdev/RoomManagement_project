using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RoomManagement.Business.Interfaces;
using RoomManagement.Business.Errors;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Data;

public sealed class PropertyStore : IPropertyStore
{
  private readonly RoomManagementDbContext _db;

  public PropertyStore(RoomManagementDbContext db)
  {
    _db = db;
  }
  public async Task AddAsync(Property property, CancellationToken cancellationToken)
  {
    _db.Properties.Add(property);
    await _db.SaveChangesAsync(cancellationToken);
  }

  public Task<Property?> GetAsync(Guid propertyId, string ownerId, CancellationToken ct) =>
    _db.Properties.SingleOrDefaultAsync(p => p.Id == propertyId && p.OwnerUserId == ownerId, ct);

  public async Task<(IReadOnlyList<Property> Items, long TotalItems)> ListAsync(string ownerId, string? search, bool? isActive, int skip, int take, CancellationToken ct)
  {
    var query = _db.Properties.AsNoTracking().Where(p => p.OwnerUserId == ownerId);
    if (!string.IsNullOrWhiteSpace(search))
      query = query.Where(p => p.Name.Contains(search) || p.Address.Contains(search));
    if (isActive.HasValue)
      query = query.Where(p => p.IsActive == isActive.Value);
    var total = await query.LongCountAsync(ct);
    var items = await query.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id)
      .Skip(skip).Take(take).ToListAsync(ct);
    return (items, total);
  }

  public async Task SaveAsync(CancellationToken ct)
  {
    try { await _db.SaveChangesAsync(ct); }
    catch (DbUpdateConcurrencyException ex)
    {
      throw new BusinessException(BusinessErrorType.NotFound, "PROPERTY_NOT_FOUND", "Không tìm thấy khu trọ.", ex);
    }
  }

  public async Task<string?> GetDeleteBlockerAsync(Guid propertyId, CancellationToken ct)
  {
    if (await _db.Rooms.AnyAsync(r => r.PropertyId == propertyId, ct))
      return "PROPERTY_HAS_ROOMS";
    if (await _db.UtilityRates.AnyAsync(rate => rate.PropertyId == propertyId, ct))
      return "PROPERTY_HAS_UTILITY_RATES";
    return null;
  }

  public async Task DeleteAsync(Property property, CancellationToken ct)
  {
    _db.Properties.Remove(property);
    try { await _db.SaveChangesAsync(ct); }
    catch (DbUpdateConcurrencyException ex)
    {
      throw new BusinessException(BusinessErrorType.NotFound, "PROPERTY_NOT_FOUND", "Không tìm thấy khu trọ.", ex);
    }
    catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 547 })
    {
      throw new BusinessException(BusinessErrorType.Conflict, "RESOURCE_HAS_DEPENDENCIES", "Khu trọ vừa phát sinh dữ liệu liên quan.", ex);
    }
  }
}
