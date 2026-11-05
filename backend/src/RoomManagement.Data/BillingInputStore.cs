using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RoomManagement.Business.DTOs;
using RoomManagement.Business.Errors;
using RoomManagement.Business.Interfaces;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;
using RoomManagement.Domain;

namespace RoomManagement.Data;

public sealed class BillingInputStore : IBillingInputStore
{
    private readonly RoomManagementDbContext _db;
    public BillingInputStore(RoomManagementDbContext db) => _db = db;

    /// <inheritdoc />
    public Task<bool> PropertyOwnedAsync(Guid propertyId, string ownerId, CancellationToken ct) =>
        _db.Properties.AnyAsync(p => p.Id == propertyId && p.OwnerUserId == ownerId && p.IsActive, ct);

    /// <inheritdoc />
    public Task<bool> RoomOwnedAsync(Guid propertyId, Guid roomId, string ownerId, CancellationToken ct) =>
        _db.Rooms.AnyAsync(r => r.Id == roomId && r.PropertyId == propertyId &&
            _db.Properties.Any(p => p.Id == propertyId && p.OwnerUserId == ownerId && p.IsActive), ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<UtilityRate>> ListRatesAsync(Guid propertyId, UtilityType? type, CancellationToken ct)
    {
        var query = _db.UtilityRates.AsNoTracking().Where(x => x.PropertyId == propertyId);
        if (type.HasValue) query = query.Where(x => x.UtilityType == type.Value);
        return await query.OrderBy(x => x.UtilityType).ThenByDescending(x => x.EffectiveFrom).ToListAsync(ct);
    }

    /// <inheritdoc />
    public Task<UtilityRate?> RateAtAsync(Guid propertyId, UtilityType type, DateOnly date, CancellationToken ct) =>
        _db.UtilityRates.AsNoTracking().SingleOrDefaultAsync(x => x.PropertyId == propertyId &&
            x.UtilityType == type && x.EffectiveFrom <= date &&
            (x.EffectiveTo == null || x.EffectiveTo >= date), ct);

    /// <inheritdoc />
    public async Task AddRateAsync(UtilityRate rate, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var overlap = await _db.UtilityRates.AnyAsync(x => x.PropertyId == rate.PropertyId &&
            x.UtilityType == rate.UtilityType &&
            (rate.EffectiveTo == null || x.EffectiveFrom <= rate.EffectiveTo.Value) &&
            (x.EffectiveTo == null || x.EffectiveTo.Value >= rate.EffectiveFrom), ct);
        if (overlap) throw Conflict("UTILITY_RATE_OVERLAP", "Khoảng hiệu lực giá bị chồng lấn.");
        _db.UtilityRates.Add(rate);
        await SaveAsync("UTILITY_RATE_CONFLICT", ct);
        await tx.CommitAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MeterReading>> ListReadingsAsync(Guid roomId, UtilityType? type, CancellationToken ct)
    {
        var query = _db.MeterReadings.AsNoTracking().Where(x => x.RoomId == roomId);
        if (type.HasValue) query = query.Where(x => x.MeterType == type.Value);
        return await query.OrderByDescending(x => x.Year).ThenByDescending(x => x.Month).ToListAsync(ct);
    }

    /// <inheritdoc />
    public Task<MeterReading?> ReadingAtAsync(Guid roomId, UtilityType type, short year, byte month, CancellationToken ct) =>
        _db.MeterReadings.AsNoTracking().SingleOrDefaultAsync(x => x.RoomId == roomId &&
            x.MeterType == type && x.Year == year && x.Month == month, ct);

    /// <inheritdoc />
    public Task<MeterReading?> LatestReadingBeforeAsync(Guid roomId, UtilityType type, short year, byte month, CancellationToken ct) =>
        _db.MeterReadings.AsNoTracking().Where(x => x.RoomId == roomId && x.MeterType == type &&
            (x.Year < year || (x.Year == year && x.Month < month)))
            .OrderByDescending(x => x.Year).ThenByDescending(x => x.Month).FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    public Task<MeterReading?> NextReadingAfterAsync(Guid roomId, UtilityType type, short year, byte month, CancellationToken ct) =>
        _db.MeterReadings.AsNoTracking().Where(x => x.RoomId == roomId && x.MeterType == type &&
            (x.Year > year || (x.Year == year && x.Month > month)))
            .OrderBy(x => x.Year).ThenBy(x => x.Month).FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    public async Task AddReadingAsync(MeterReading reading, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var previous = await LatestReadingBeforeAsync(reading.RoomId, reading.MeterType, reading.Year, reading.Month, ct);
        var next = await NextReadingAfterAsync(reading.RoomId, reading.MeterType, reading.Year, reading.Month, ct);
        var period = new DateOnly(reading.Year, reading.Month, 1);
        if (previous is not null &&
            (new DateOnly(previous.Year, previous.Month, 1).AddMonths(1) != period || previous.CurrentValue != reading.PreviousValue))
            throw Conflict("METER_READING_GAP", "Chỉ số không nối tiếp kỳ trước.");
        if (next is not null &&
            (period.AddMonths(1) != new DateOnly(next.Year, next.Month, 1) || reading.CurrentValue != next.PreviousValue))
            throw Conflict("METER_READING_GAP", "Chỉ số không nối tiếp kỳ sau.");
        _db.MeterReadings.Add(reading);
        await SaveAsync("METER_READING_CONFLICT", ct);
        await tx.CommitAsync(ct);
    }

    /// <inheritdoc />
    public Task<bool> ActiveAdminAsync(string userId, CancellationToken ct) =>
        _db.Users.AnyAsync(x => x.Id == userId && x.Role == UserRoles.Admin && x.IsActive, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Service>> ListServicesAsync(CancellationToken ct) =>
        await _db.Services.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct);

    /// <inheritdoc />
    public Task<Service?> GetServiceAsync(Guid serviceId, CancellationToken ct) =>
        _db.Services.AsNoTracking().SingleOrDefaultAsync(x => x.Id == serviceId, ct);

    /// <inheritdoc />
    public async Task AddServiceAsync(Service service, CancellationToken ct)
    {
        _db.Services.Add(service);
        await SaveAsync("SERVICE_NAME_EXISTS", ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RoomServiceResult>> ListRoomServicesAsync(Guid roomId, CancellationToken ct) =>
        await (from assignment in _db.RoomServices.AsNoTracking()
               join service in _db.Services.AsNoTracking() on assignment.ServiceId equals service.Id
               where assignment.RoomId == roomId
               orderby assignment.StartDate descending, service.Name
               select new RoomServiceResult(assignment, service)).ToListAsync(ct);

    /// <inheritdoc />
    public async Task AddRoomServiceAsync(RoomService assignment, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var overlap = await _db.RoomServices.AnyAsync(x => x.RoomId == assignment.RoomId &&
            x.ServiceId == assignment.ServiceId &&
            (assignment.EndDate == null || x.StartDate <= assignment.EndDate.Value) &&
            (x.EndDate == null || x.EndDate.Value >= assignment.StartDate), ct);
        if (overlap) throw Conflict("ROOM_SERVICE_OVERLAP", "Dịch vụ đã được gán trong khoảng thời gian này.");
        _db.RoomServices.Add(assignment);
        await SaveAsync("ROOM_SERVICE_CONFLICT", ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>Lưu thay đổi và chuyển lỗi trùng dữ liệu thành lỗi nghiệp vụ.</summary>
    private async Task SaveAsync(string code, CancellationToken ct)
    {
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new BusinessException(BusinessErrorType.Conflict, code, "Dữ liệu đã tồn tại.", ex);
        }
    }

    /// <summary>Tạo lỗi nghiệp vụ khi dữ liệu bị xung đột.</summary>
    private static BusinessException Conflict(string code, string message) =>
        new(BusinessErrorType.Conflict, code, message);
}
