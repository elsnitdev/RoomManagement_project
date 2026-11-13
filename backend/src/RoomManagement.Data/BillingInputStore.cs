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

    public async Task ReplaceRateAsync(UtilityRate rate, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var invoiceExists = await (from invoice in _db.Invoices
            join room in _db.Rooms on invoice.RoomId equals room.Id
            where room.PropertyId == rate.PropertyId && invoice.InvoiceType == InvoiceType.Monthly &&
                invoice.Status != InvoiceStatus.Cancelled && invoice.BillingYear != null && invoice.BillingMonth != null &&
                (invoice.BillingYear > rate.EffectiveFrom.Year ||
                 invoice.BillingYear == rate.EffectiveFrom.Year && invoice.BillingMonth >= rate.EffectiveFrom.Month)
            select invoice.Id).AnyAsync(ct);
        if (invoiceExists) throw Conflict("UTILITY_RATE_INVOICE_EXISTS", "Kỳ áp dụng đã có hóa đơn.");
        var current = await _db.UtilityRates.SingleOrDefaultAsync(x => x.PropertyId == rate.PropertyId &&
            x.UtilityType == rate.UtilityType && x.EffectiveFrom < rate.EffectiveFrom &&
            (x.EffectiveTo == null || x.EffectiveTo >= rate.EffectiveFrom), ct);
        if (current is null) throw Conflict("UTILITY_RATE_NOT_FOUND", "Không có giá hiện hành để thay thế.");
        if (await _db.UtilityRates.AnyAsync(x => x.PropertyId == rate.PropertyId &&
            x.UtilityType == rate.UtilityType && x.Id != current.Id && x.EffectiveFrom >= rate.EffectiveFrom, ct))
            throw Conflict("UTILITY_RATE_OVERLAP", "Đã có giá cho kỳ này hoặc kỳ sau.");
        current.EffectiveTo = rate.EffectiveFrom.AddDays(-1);
        await _db.SaveChangesAsync(ct);
        _db.UtilityRates.Add(rate);
        await SaveAsync("UTILITY_RATE_CONFLICT", ct);
        await tx.CommitAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MeterReading>> ListReadingsAsync(Guid roomId, UtilityType? type, CancellationToken ct)
    {
        var query = _db.MeterReadings.AsNoTracking().Where(x => x.RoomId == roomId && x.ContractId == null);
        if (type.HasValue) query = query.Where(x => x.MeterType == type.Value);
        return await query.OrderByDescending(x => x.Year).ThenByDescending(x => x.Month).ToListAsync(ct);
    }

    /// <inheritdoc />
    public Task<MeterReading?> ReadingAtAsync(Guid roomId, UtilityType type, short year, byte month, CancellationToken ct) =>
        _db.MeterReadings.AsNoTracking().SingleOrDefaultAsync(x => x.RoomId == roomId && x.ContractId == null &&
            x.MeterType == type && x.Year == year && x.Month == month, ct);

    public async Task<MeterReading?> ReadingForContractAsync(Contract contract, UtilityType type,
        short year, byte month, CancellationToken ct)
    {
        var scoped = await _db.MeterReadings.AsNoTracking().SingleOrDefaultAsync(x =>
            x.ContractId == contract.Id && x.MeterType == type && x.Year == year && x.Month == month, ct);
        if (scoped is not null) return scoped;
        var periodStart = new DateOnly(year, month, 1);
        var periodEnd = periodStart.AddMonths(1).AddDays(-1);
        var shared = await _db.Contracts.AnyAsync(x => x.Id != contract.Id && x.RoomId == contract.RoomId &&
            x.Status != ContractStatus.Cancelled && x.Status != ContractStatus.Draft &&
            x.StartDate <= periodEnd && (x.HandoverOn ?? x.EndDate ?? DateOnly.MaxValue) >= periodStart, ct);
        return shared ? null : await ReadingAtAsync(contract.RoomId, type, year, month, ct);
    }

    /// <inheritdoc />
    public Task<MeterReading?> LatestReadingBeforeAsync(Guid roomId, UtilityType type, short year, byte month, CancellationToken ct) =>
        _db.MeterReadings.AsNoTracking().Where(x => x.RoomId == roomId && x.ContractId == null && x.MeterType == type &&
            (x.Year < year || (x.Year == year && x.Month < month)))
            .OrderByDescending(x => x.Year).ThenByDescending(x => x.Month).FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    public Task<MeterReading?> NextReadingAfterAsync(Guid roomId, UtilityType type, short year, byte month, CancellationToken ct) =>
        _db.MeterReadings.AsNoTracking().Where(x => x.RoomId == roomId && x.ContractId == null && x.MeterType == type &&
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
    public async Task<IReadOnlyList<Service>> ListServicesAsync(Guid propertyId, CancellationToken ct) =>
        await _db.Services.AsNoTracking().Where(x => x.PropertyId == propertyId).OrderBy(x => x.Name).ToListAsync(ct);

    /// <inheritdoc />
    public Task<Service?> GetServiceAsync(Guid propertyId, Guid serviceId, CancellationToken ct) =>
        _db.Services.AsNoTracking().SingleOrDefaultAsync(x => x.Id == serviceId && x.PropertyId == propertyId, ct);

    /// <inheritdoc />
    public async Task AddServiceAsync(Service service, CancellationToken ct)
    {
        _db.Services.Add(service);
        await SaveAsync("SERVICE_NAME_EXISTS", ct);
    }

    public async Task<Service> UpdateServiceAsync(Guid propertyId, Guid serviceId, string name,
        string? description, string? unit, decimal? defaultPrice, bool isActive, CancellationToken ct)
    {
        var service = await _db.Services.SingleOrDefaultAsync(x => x.Id == serviceId && x.PropertyId == propertyId, ct)
            ?? throw new BusinessException(BusinessErrorType.NotFound, "SERVICE_NOT_FOUND", "Không tìm thấy dịch vụ của khu trọ.");
        service.Name = name; service.Description = description; service.Unit = unit;
        service.DefaultPrice = defaultPrice; service.IsActive = isActive;
        await SaveAsync("SERVICE_NAME_EXISTS", ct);
        return service;
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
        var hasInvoice = await _db.Invoices.AnyAsync(x => x.RoomId == assignment.RoomId &&
            x.InvoiceType == InvoiceType.Monthly && x.Status != InvoiceStatus.Cancelled &&
            x.BillingYear != null && x.BillingMonth != null &&
            (x.BillingYear > assignment.StartDate.Year ||
             x.BillingYear == assignment.StartDate.Year && x.BillingMonth >= assignment.StartDate.Month), ct);
        if (hasInvoice) throw Conflict("ROOM_SERVICE_INVOICE_EXISTS", "Kỳ áp dụng đã có hóa đơn.");
        var overlap = await _db.RoomServices.AnyAsync(x => x.RoomId == assignment.RoomId &&
            x.ServiceId == assignment.ServiceId &&
            (assignment.EndDate == null || x.StartDate <= assignment.EndDate.Value) &&
            (x.EndDate == null || x.EndDate.Value >= assignment.StartDate), ct);
        if (overlap) throw Conflict("ROOM_SERVICE_OVERLAP", "Dịch vụ đã được gán trong khoảng thời gian này.");
        _db.RoomServices.Add(assignment);
        await SaveAsync("ROOM_SERVICE_CONFLICT", ct);
        await tx.CommitAsync(ct);
    }

    public async Task<RoomService> ReplaceRoomServiceAsync(Guid propertyId, Guid roomId, Guid assignmentId,
        RoomService replacement, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var existing = await _db.RoomServices.SingleOrDefaultAsync(x => x.Id == assignmentId &&
            x.RoomId == roomId && x.ServiceId == replacement.ServiceId, ct)
            ?? throw new BusinessException(BusinessErrorType.NotFound, "ROOM_SERVICE_NOT_FOUND", "Không tìm thấy dịch vụ của phòng.");
        if (existing.StartDate >= replacement.StartDate ||
            existing.EndDate is { } end && end < replacement.StartDate)
            throw Conflict("ROOM_SERVICE_OVERLAP", "Kỳ áp dụng dịch vụ không hợp lệ.");
        if (await _db.RoomServices.AnyAsync(x => x.RoomId == roomId &&
            x.ServiceId == replacement.ServiceId && x.Id != existing.Id &&
            (x.EndDate == null || x.EndDate >= replacement.StartDate), ct))
            throw Conflict("ROOM_SERVICE_OVERLAP", "Đã có phiên bản dịch vụ khác cho kỳ này.");
        var hasInvoice = await (from invoice in _db.Invoices
            join room in _db.Rooms on invoice.RoomId equals room.Id
            where room.Id == roomId && room.PropertyId == propertyId &&
                invoice.InvoiceType == InvoiceType.Monthly && invoice.Status != InvoiceStatus.Cancelled &&
                invoice.BillingYear != null && invoice.BillingMonth != null &&
                (invoice.BillingYear > replacement.StartDate.Year ||
                 invoice.BillingYear == replacement.StartDate.Year && invoice.BillingMonth >= replacement.StartDate.Month)
            select invoice.Id).AnyAsync(ct);
        if (hasInvoice) throw Conflict("ROOM_SERVICE_INVOICE_EXISTS", "Kỳ áp dụng đã có hóa đơn.");
        existing.EndDate = replacement.StartDate.AddDays(-1);
        await _db.SaveChangesAsync(ct);
        _db.RoomServices.Add(replacement);
        await SaveAsync("ROOM_SERVICE_CONFLICT", ct);
        await tx.CommitAsync(ct);
        return replacement;
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
