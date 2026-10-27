using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RoomManagement.Business.DTOs;
using RoomManagement.Business.Errors;
using RoomManagement.Business.Interfaces;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Data;

public sealed class RoomStore : IRoomStore
{
    private readonly RoomManagementDbContext _db;
    public RoomStore(RoomManagementDbContext db) => _db = db;

    private IQueryable<Room> OwnedRooms(Guid propertyId, string ownerId) =>
        from room in _db.Rooms
        join property in _db.Properties on room.PropertyId equals property.Id
        where room.PropertyId == propertyId && property.OwnerUserId == ownerId
        select room;

    public async Task AddAsync(Room room, CancellationToken ct)
    {
        _db.Rooms.Add(room);
        await SaveAsync(ct);
    }

    public Task<Room?> GetAsync(Guid propertyId, Guid roomId, string ownerId, CancellationToken ct) =>
        OwnedRooms(propertyId, ownerId).SingleOrDefaultAsync(r => r.Id == roomId, ct);

    public async Task<RoomResult?> GetResultAsync(Guid propertyId, Guid roomId, string ownerId, CancellationToken ct)
    {
        var row = await OwnedRooms(propertyId, ownerId).AsNoTracking()
            .Where(r => r.Id == roomId)
            .Select(r => new
            {
                Room = r,
                HasActiveContract = _db.Contracts.Any(c => c.RoomId == r.Id && c.Status == ContractStatus.Active)
            })
            .SingleOrDefaultAsync(ct);
        return row is null ? null : new RoomResult(row.Room, EffectiveStatus(row.Room.Status, row.HasActiveContract));
    }

    public async Task<(IReadOnlyList<RoomResult> Items, long TotalItems)> ListAsync(Guid propertyId, string ownerId, string? search, RoomStatus? status, int skip, int take, CancellationToken ct)
    {
        var query = OwnedRooms(propertyId, ownerId).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(r => r.RoomNumber.Contains(search) || (r.RoomName != null && r.RoomName.Contains(search)));
        if (status.HasValue)
        {
            query = status.Value switch
            {
                RoomStatus.Occupied => query.Where(r => _db.Contracts.Any(c => c.RoomId == r.Id && c.Status == ContractStatus.Active)),
                RoomStatus.Available => query.Where(r => !_db.Contracts.Any(c => c.RoomId == r.Id && c.Status == ContractStatus.Active)
                    && (r.Status == RoomStatus.Available || r.Status == RoomStatus.Occupied)),
                _ => query.Where(r => r.Status == status.Value
                    && !_db.Contracts.Any(c => c.RoomId == r.Id && c.Status == ContractStatus.Active))
            };
        }
        var total = await query.LongCountAsync(ct);
        var rows = await query.OrderBy(r => r.RoomNumber).ThenBy(r => r.Id)
            .Skip(skip).Take(take)
            .Select(r => new
            {
                Room = r,
                HasActiveContract = _db.Contracts.Any(c => c.RoomId == r.Id && c.Status == ContractStatus.Active)
            })
            .ToListAsync(ct);
        return (rows.Select(row => new RoomResult(row.Room, EffectiveStatus(row.Room.Status, row.HasActiveContract))).ToList(), total);
    }

    private static RoomStatus EffectiveStatus(RoomStatus stored, bool hasActiveContract) =>
        hasActiveContract ? RoomStatus.Occupied
            : stored == RoomStatus.Occupied ? RoomStatus.Available : stored;

    public Task<bool> NumberExistsAsync(Guid propertyId, string number, Guid? exceptRoomId, CancellationToken ct) =>
        _db.Rooms.AnyAsync(r => r.PropertyId == propertyId && r.RoomNumber == number && (!exceptRoomId.HasValue || r.Id != exceptRoomId.Value), ct);

    public Task<bool> HasActiveContractAsync(Guid roomId, CancellationToken ct) =>
        _db.Contracts.AnyAsync(c => c.RoomId == roomId && c.Status == ContractStatus.Active, ct);

    public async Task<string?> GetDeleteBlockerAsync(Guid roomId, CancellationToken ct)
    {
        if (await _db.Contracts.AnyAsync(c => c.RoomId == roomId, ct)) return "ROOM_HAS_CONTRACTS";
        if (await _db.RoomServices.AnyAsync(s => s.RoomId == roomId, ct)) return "ROOM_HAS_SERVICES";
        if (await _db.MeterReadings.AnyAsync(m => m.RoomId == roomId, ct)) return "ROOM_HAS_METER_READINGS";
        return null;
    }

    public async Task SaveAsync(CancellationToken ct)
    {
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new BusinessException(BusinessErrorType.Conflict, "ROW_VERSION_CONFLICT", "Phòng đã thay đổi hoặc bị xóa. Tải lại dữ liệu trước khi cập nhật.", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql
            && sql.Number is 2601 or 2627 && sql.Message.Contains("UX_Rooms_Property_Number", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException(BusinessErrorType.Conflict, "ROOM_NUMBER_ALREADY_EXISTS", "Mã phòng đã tồn tại trong khu trọ.", ex);
        }
    }

    public async Task DeleteAsync(Room room, CancellationToken ct)
    {
        _db.Rooms.Remove(room);
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new BusinessException(BusinessErrorType.NotFound, "ROOM_NOT_FOUND", "Không tìm thấy phòng.", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 547 })
        {
            throw new BusinessException(BusinessErrorType.Conflict, "RESOURCE_HAS_DEPENDENCIES", "Phòng vừa phát sinh dữ liệu liên quan.", ex);
        }
    }
}
