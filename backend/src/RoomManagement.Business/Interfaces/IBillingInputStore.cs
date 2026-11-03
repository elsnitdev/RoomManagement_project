using RoomManagement.Business.DTOs;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Business.Interfaces;

public interface IBillingInputStore
{
    Task<bool> PropertyOwnedAsync(Guid propertyId, string ownerId, CancellationToken ct);
    Task<bool> RoomOwnedAsync(Guid propertyId, Guid roomId, string ownerId, CancellationToken ct);
    Task<IReadOnlyList<UtilityRate>> ListRatesAsync(Guid propertyId, UtilityType? type, CancellationToken ct);
    Task<UtilityRate?> RateAtAsync(Guid propertyId, UtilityType type, DateOnly date, CancellationToken ct);
    Task AddRateAsync(UtilityRate rate, CancellationToken ct);
    Task<IReadOnlyList<MeterReading>> ListReadingsAsync(Guid roomId, UtilityType? type, CancellationToken ct);
    Task<MeterReading?> ReadingAtAsync(Guid roomId, UtilityType type, short year, byte month, CancellationToken ct);
    Task<MeterReading?> LatestReadingBeforeAsync(Guid roomId, UtilityType type, short year, byte month, CancellationToken ct);
    Task<MeterReading?> NextReadingAfterAsync(Guid roomId, UtilityType type, short year, byte month, CancellationToken ct);
    Task AddReadingAsync(MeterReading reading, CancellationToken ct);
    Task<bool> ActiveAdminAsync(string userId, CancellationToken ct);
    Task<IReadOnlyList<Service>> ListServicesAsync(CancellationToken ct);
    Task<Service?> GetServiceAsync(Guid serviceId, CancellationToken ct);
    Task AddServiceAsync(Service service, CancellationToken ct);
    Task<IReadOnlyList<RoomServiceResult>> ListRoomServicesAsync(Guid roomId, CancellationToken ct);
    Task AddRoomServiceAsync(RoomService assignment, CancellationToken ct);
}
