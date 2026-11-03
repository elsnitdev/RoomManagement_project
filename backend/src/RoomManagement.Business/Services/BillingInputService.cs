using RoomManagement.Business.DTOs;
using RoomManagement.Business.Errors;
using RoomManagement.Business.Interfaces;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;
using RoomServiceEntity = RoomManagement.Domain.Entities.RoomService;

namespace RoomManagement.Business.Services;

public sealed class BillingInputService
{
    private readonly ICurrentLandlord _current;
    private readonly ILandlordStore _landlords;
    private readonly IBillingInputStore _store;

    public BillingInputService(ICurrentLandlord current, ILandlordStore landlords, IBillingInputStore store)
    {
        _current = current;
        _landlords = landlords;
        _store = store;
    }

    private async Task<string> OwnerAsync(CancellationToken ct)
    {
        var owner = _current.GetUserId();
        if (!await _landlords.IsActiveLandlordAsync(owner, ct))
            throw Invalid("LANDLORD_UNAVAILABLE", "Chủ trọ không khả dụng.");
        return owner;
    }

    private async Task EnsurePropertyAsync(Guid propertyId, string owner, CancellationToken ct)
    {
        if (!await _store.PropertyOwnedAsync(propertyId, owner, ct))
            throw NotFound("PROPERTY_NOT_FOUND", "Không tìm thấy khu trọ đang hoạt động.");
    }

    private async Task EnsureRoomAsync(Guid propertyId, Guid roomId, string owner, CancellationToken ct)
    {
        if (!await _store.RoomOwnedAsync(propertyId, roomId, owner, ct))
            throw NotFound("ROOM_NOT_FOUND", "Không tìm thấy phòng trong khu trọ đang hoạt động.");
    }

    public async Task<UtilityRate> CreateRateAsync(Guid propertyId, CreateUtilityRateInput input, CancellationToken ct)
    {
        var owner = await OwnerAsync(ct);
        await EnsurePropertyAsync(propertyId, owner, ct);
        if (!Enum.IsDefined(input.Type) || input.UnitPrice < 0 || input.UnitPrice != decimal.Round(input.UnitPrice, 2) ||
            string.IsNullOrWhiteSpace(input.Unit) || input.Unit.Trim().Length > 50 ||
            input.EffectiveFrom == default || input.EffectiveTo is { } end && end < input.EffectiveFrom)
            throw Validation("UTILITY_RATE_INVALID", "Đơn giá điện nước không hợp lệ.");
        var rate = new UtilityRate
        {
            Id = Guid.NewGuid(), PropertyId = propertyId, UtilityType = input.Type,
            UnitPrice = input.UnitPrice, Unit = input.Unit.Trim(),
            EffectiveFrom = input.EffectiveFrom, EffectiveTo = input.EffectiveTo,
            CreatedAt = DateTime.UtcNow
        };
        await _store.AddRateAsync(rate, ct);
        return rate;
    }

    public async Task<IReadOnlyList<UtilityRate>> ListRatesAsync(Guid propertyId, UtilityType? type, CancellationToken ct)
    {
        var owner = await OwnerAsync(ct);
        await EnsurePropertyAsync(propertyId, owner, ct);
        if (type.HasValue && !Enum.IsDefined(type.Value))
            throw Validation("UTILITY_TYPE_INVALID", "Loại tiện ích không hợp lệ.");
        return await _store.ListRatesAsync(propertyId, type, ct);
    }

    public async Task<MeterReading> CreateReadingAsync(Guid propertyId, Guid roomId, CreateMeterReadingInput input, CancellationToken ct)
    {
        var owner = await OwnerAsync(ct);
        await EnsureRoomAsync(propertyId, roomId, owner, ct);
        if (!Enum.IsDefined(input.Type) || input.Year is < 2000 or > 2100 || input.Month is < 1 or > 12 ||
            input.PreviousValue < 0 || input.CurrentValue < input.PreviousValue ||
            input.PreviousValue != decimal.Round(input.PreviousValue, 2) ||
            input.CurrentValue != decimal.Round(input.CurrentValue, 2) || input.Note is { Length: > 500 })
            throw Validation("METER_READING_INVALID", "Chỉ số hoặc kỳ không hợp lệ.");
        var reading = new MeterReading
        {
            Id = Guid.NewGuid(), RoomId = roomId, MeterType = input.Type,
            Year = input.Year, Month = input.Month, PreviousValue = input.PreviousValue,
            CurrentValue = input.CurrentValue, Note = input.Note?.Trim(), RecordedAt = DateTime.UtcNow
        };
        await _store.AddReadingAsync(reading, ct);
        return reading;
    }

    public async Task<IReadOnlyList<MeterReading>> ListReadingsAsync(Guid propertyId, Guid roomId, UtilityType? type, CancellationToken ct)
    {
        var owner = await OwnerAsync(ct);
        await EnsureRoomAsync(propertyId, roomId, owner, ct);
        if (type.HasValue && !Enum.IsDefined(type.Value))
            throw Validation("UTILITY_TYPE_INVALID", "Loại tiện ích không hợp lệ.");
        return await _store.ListReadingsAsync(roomId, type, ct);
    }

    public Task<IReadOnlyList<Service>> ListServicesAsync(CancellationToken ct) => _store.ListServicesAsync(ct);

    public async Task<Service> CreateServiceAsync(string actorId, CreateServiceInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(actorId) || !await _store.ActiveAdminAsync(actorId, ct))
            throw new BusinessException(BusinessErrorType.NotFound, "ADMIN_NOT_FOUND", "Không tìm thấy quản trị viên.");
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length > 100 ||
            input.Description is { Length: > 500 } || input.Unit is { Length: > 50 } ||
            input.DefaultPrice is < 0 ||
            input.DefaultPrice is { } price && price != decimal.Round(price, 2))
            throw Validation("SERVICE_INVALID", "Thông tin dịch vụ không hợp lệ.");
        var service = new Service
        {
            Id = Guid.NewGuid(), Name = input.Name.Trim(), Description = input.Description?.Trim(),
            Unit = input.Unit?.Trim(), DefaultPrice = input.DefaultPrice, IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        await _store.AddServiceAsync(service, ct);
        return service;
    }

    public async Task<RoomServiceResult> AssignRoomServiceAsync(Guid propertyId, Guid roomId, AssignRoomServiceInput input, CancellationToken ct)
    {
        var owner = await OwnerAsync(ct);
        await EnsureRoomAsync(propertyId, roomId, owner, ct);
        var service = await _store.GetServiceAsync(input.ServiceId, ct);
        if (service is null || !service.IsActive)
            throw NotFound("SERVICE_NOT_FOUND", "Không tìm thấy dịch vụ đang hoạt động.");
        if (input.StartDate == default || input.EndDate is { } end && end < input.StartDate ||
            input.CustomPrice is < 0 ||
            input.CustomPrice is { } price && price != decimal.Round(price, 2) ||
            input.CustomPrice is null && service.DefaultPrice is null)
            throw Validation("ROOM_SERVICE_INVALID", "Thời gian hoặc đơn giá dịch vụ không hợp lệ.");
        var assignment = new RoomServiceEntity
        {
            Id = Guid.NewGuid(), RoomId = roomId, ServiceId = input.ServiceId,
            CustomPrice = input.CustomPrice, StartDate = input.StartDate,
            EndDate = input.EndDate, CreatedAt = DateTime.UtcNow
        };
        await _store.AddRoomServiceAsync(assignment, ct);
        return new RoomServiceResult(assignment, service);
    }

    public async Task<IReadOnlyList<RoomServiceResult>> ListRoomServicesAsync(Guid propertyId, Guid roomId, CancellationToken ct)
    {
        var owner = await OwnerAsync(ct);
        await EnsureRoomAsync(propertyId, roomId, owner, ct);
        return await _store.ListRoomServicesAsync(roomId, ct);
    }

    private static BusinessException Validation(string code, string message) => new(BusinessErrorType.Validation, code, message);
    private static BusinessException Invalid(string code, string message) => new(BusinessErrorType.InvalidOperation, code, message);
    private static BusinessException NotFound(string code, string message) => new(BusinessErrorType.NotFound, code, message);
}
