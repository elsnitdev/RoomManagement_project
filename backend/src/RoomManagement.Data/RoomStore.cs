using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RoomManagement.Business.DTOs;
using RoomManagement.Business.Errors;
using RoomManagement.Business.Interfaces;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;
using RoomManagement.Business.Services;

namespace RoomManagement.Data;

public sealed class RoomStore : IRoomStore
{
    private readonly RoomManagementDbContext _db;
    public RoomStore(RoomManagementDbContext db) => _db = db;

    /// <summary>Tạo truy vấn phòng thuộc khu trọ của chủ trọ.</summary>
    private IQueryable<Room> OwnedRooms(Guid propertyId, string ownerId) =>
        from room in _db.Rooms
        join property in _db.Properties on room.PropertyId equals property.Id
        where room.PropertyId == propertyId && property.OwnerUserId == ownerId
        select room;

    /// <inheritdoc />
    public async Task AddAsync(Room room, CancellationToken ct)
    {
        _db.Rooms.Add(room);
        await SaveAsync(ct);
    }

    /// <inheritdoc />
    public Task<Room?> GetAsync(Guid propertyId, Guid roomId, string ownerId, CancellationToken ct) =>
        OwnedRooms(propertyId, ownerId).SingleOrDefaultAsync(r => r.Id == roomId, ct);

    /// <inheritdoc />
    public async Task<RoomResult?> GetResultAsync(Guid propertyId, Guid roomId, string ownerId, CancellationToken ct)
    {
        var today = VietnamDate.Today;
        var row = await OwnedRooms(propertyId, ownerId).AsNoTracking()
            .Where(r => r.Id == roomId)
            .Select(r => new
            {
                Room = r,
                HasCurrentContract = _db.Contracts.Any(c => c.RoomId == r.Id &&
                    c.Status == ContractStatus.Active && c.StartDate <= today &&
                    (c.EndDate == null || c.EndDate >= today)),
                ContractId = _db.Contracts.Where(c => c.RoomId == r.Id && c.Status == ContractStatus.Active &&
                    (c.EndDate == null || c.EndDate >= today))
                    .OrderBy(c => c.StartDate)
                    .Select(c => (Guid?)c.Id).FirstOrDefault()
            })
            .SingleOrDefaultAsync(ct);
        if (row is null) return null;
        var occupants = row.ContractId.HasValue ? await OccupantsAsync(row.ContractId.Value, today, ct) : [];
        var needsContract = !row.HasCurrentContract && await NeedsContractAsync(row.Room.Id, today, ct);
        return new RoomResult(row.Room, EffectiveStatus(row.Room.Status, occupants.Count > 0), row.ContractId,
            occupants, OccupancyState(row.ContractId, occupants.Count, needsContract));
    }

    /// <summary>Cảnh báo phòng có thành viên của hợp đồng cũ chưa ghi ngày rời nhưng chưa có hợp đồng hiện hành.</summary>
    private Task<bool> NeedsContractAsync(Guid roomId, DateOnly today, CancellationToken ct) =>
        _db.Contracts.AnyAsync(contract => contract.RoomId == roomId &&
            (contract.Status == ContractStatus.Active || contract.Status == ContractStatus.Expired ||
             contract.Status == ContractStatus.Terminated) && contract.StartDate <= today &&
            ((contract.EndDate != null && contract.EndDate < today) ||
             (contract.ClosedOn != null && contract.ClosedOn < today)) &&
            _db.ContractTenants.Any(link => link.ContractId == contract.Id && link.LeaveDate == null), ct);

    /// <summary>Chuyển dữ liệu hợp đồng và người thuê thành trạng thái thuê phòng dùng cho API.</summary>
    private static RoomOccupancyState OccupancyState(Guid? contractId, int occupantCount, bool needsContract) =>
        occupantCount > 0 ? RoomOccupancyState.Occupied :
        needsContract ? RoomOccupancyState.NeedsContract :
        contractId.HasValue ? RoomOccupancyState.ContractedAwaitingMoveIn : RoomOccupancyState.Vacant;

    /// <summary>Lấy danh sách người đang ở theo hợp đồng của phòng.</summary>
    private async Task<IReadOnlyList<RoomOccupant>> OccupantsAsync(Guid contractId, DateOnly today, CancellationToken ct) =>
        await (from link in _db.ContractTenants.AsNoTracking()
               join profile in _db.TenantProfiles.AsNoTracking() on link.TenantId equals profile.Id
               where link.ContractId == contractId && link.JoinDate <= today &&
                     (link.LeaveDate == null || link.LeaveDate >= today) &&
                     (_db.ContractRulesAcceptances.Any(a => a.ContractTenantId == link.Id) ||
                      !link.OnlineRulesRequired && _db.MemberCommitments.Any(commitment => commitment.ContractTenantId == link.Id &&
                         commitment.CommitmentType == CommitmentType.RoomRulesAgreement &&
                         commitment.SupersededAt == null &&
                         (commitment.ConfirmedAt != null ||
                          commitment.EvidenceStatus == CommitmentEvidenceStatus.LegacyPendingEvidence)))
               orderby profile.FullName
               select new RoomOccupant(profile.Id, profile.FullName, link.JoinDate)).ToListAsync(ct);

    /// <inheritdoc />
    public async Task<(IReadOnlyList<RoomResult> Items, long TotalItems)> ListAsync(Guid propertyId, string ownerId, string? search, RoomStatus? status, Guid? rowId, int? floor, int skip, int take, CancellationToken ct)
    {
        var today = VietnamDate.Today;
        var query = OwnedRooms(propertyId, ownerId).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(r => r.RoomNumber.Contains(search) || (r.RoomName != null && r.RoomName.Contains(search)));
        if (rowId.HasValue) query = query.Where(r => r.RowId == rowId);
        if (floor.HasValue) query = query.Where(r => r.Floor == floor);
        if (status.HasValue)
        {
            query = status.Value switch
            {
                RoomStatus.Occupied => query.Where(r => _db.Contracts.Any(c => c.RoomId == r.Id && c.Status == ContractStatus.Active && c.StartDate <= today && (c.EndDate == null || c.EndDate >= today) &&
                    _db.ContractTenants.Any(t => t.ContractId == c.Id && t.JoinDate <= today &&
                        (t.LeaveDate == null || t.LeaveDate >= today) &&
                        (_db.ContractRulesAcceptances.Any(a => a.ContractTenantId == t.Id) ||
                         !t.OnlineRulesRequired && _db.MemberCommitments.Any(m => m.ContractTenantId == t.Id &&
                            m.CommitmentType == CommitmentType.RoomRulesAgreement && m.SupersededAt == null &&
                            (m.ConfirmedAt != null || m.EvidenceStatus == CommitmentEvidenceStatus.LegacyPendingEvidence)))))),
                RoomStatus.Available => query.Where(r => !_db.Contracts.Any(c => c.RoomId == r.Id &&
                    c.Status == ContractStatus.Active &&
                    (c.EndDate == null || c.EndDate >= today)) &&
                    !_db.Contracts.Any(c => c.RoomId == r.Id &&
                        (c.Status == ContractStatus.Active || c.Status == ContractStatus.Expired ||
                         c.Status == ContractStatus.Terminated) && c.StartDate <= today &&
                        ((c.EndDate != null && c.EndDate < today) ||
                         (c.ClosedOn != null && c.ClosedOn < today)) &&
                        _db.ContractTenants.Any(t => t.ContractId == c.Id && t.LeaveDate == null)) &&
                    (r.Status == RoomStatus.Available || r.Status == RoomStatus.Occupied)),
                _ => query.Where(r => r.Status == status.Value
                    && !_db.Contracts.Any(c => c.RoomId == r.Id && c.Status == ContractStatus.Active && c.StartDate <= today && (c.EndDate == null || c.EndDate >= today)))
            };
        }
        var total = await query.LongCountAsync(ct);
        var rows = await query.OrderBy(r => r.RoomNumber).ThenBy(r => r.Id)
            .Skip(skip).Take(take)
            .Select(r => new
            {
                Room = r,
                HasCurrentContract = _db.Contracts.Any(c => c.RoomId == r.Id &&
                    c.Status == ContractStatus.Active && c.StartDate <= today &&
                    (c.EndDate == null || c.EndDate >= today)),
                ContractId = _db.Contracts.Where(c => c.RoomId == r.Id && c.Status == ContractStatus.Active &&
                    (c.EndDate == null || c.EndDate >= today)).OrderBy(c => c.StartDate)
                    .Select(c => (Guid?)c.Id).FirstOrDefault()
            })
            .ToListAsync(ct);
        var results = new List<RoomResult>();
        foreach (var row in rows)
        {
            var occupants = row.ContractId.HasValue ? await OccupantsAsync(row.ContractId.Value, today, ct) : [];
            var needsContract = !row.HasCurrentContract && await NeedsContractAsync(row.Room.Id, today, ct);
            results.Add(new RoomResult(row.Room, EffectiveStatus(row.Room.Status, occupants.Count > 0),
                row.ContractId, occupants, OccupancyState(row.ContractId, occupants.Count, needsContract)));
        }
        return (results, total);
    }

    /// <summary>Xác định trạng thái phòng theo hợp đồng đang hoạt động.</summary>
    private static RoomStatus EffectiveStatus(RoomStatus stored, bool hasActiveContract) =>
        hasActiveContract ? RoomStatus.Occupied
            : stored == RoomStatus.Occupied ? RoomStatus.Available : stored;

    /// <inheritdoc />
    public Task<bool> NumberExistsAsync(Guid propertyId, string number, Guid? exceptRoomId, CancellationToken ct) =>
        _db.Rooms.AnyAsync(r => r.PropertyId == propertyId && r.RoomNumber == number && (!exceptRoomId.HasValue || r.Id != exceptRoomId.Value), ct);

    /// <inheritdoc />
    public Task<bool> HasActiveContractAsync(Guid roomId, CancellationToken ct) =>
        _db.Contracts.AnyAsync(c => c.RoomId == roomId && c.Status == ContractStatus.Active &&
            (c.EndDate == null || c.EndDate >= VietnamDate.Today), ct);

    /// <inheritdoc />
    public async Task<string?> GetDeleteBlockerAsync(Guid roomId, CancellationToken ct)
    {
        if (await _db.RoomImages.AnyAsync(i => i.RoomId == roomId, ct)) return "ROOM_HAS_IMAGES";
        if (await _db.Contracts.AnyAsync(c => c.RoomId == roomId, ct)) return "ROOM_HAS_CONTRACTS";
        if (await _db.RoomServices.AnyAsync(s => s.RoomId == roomId, ct)) return "ROOM_HAS_SERVICES";
        if (await _db.MeterReadings.AnyAsync(m => m.RoomId == roomId, ct)) return "ROOM_HAS_METER_READINGS";
        return null;
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
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
