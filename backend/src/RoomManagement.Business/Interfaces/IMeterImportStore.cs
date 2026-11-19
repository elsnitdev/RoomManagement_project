using RoomManagement.Business.DTOs;

namespace RoomManagement.Business.Interfaces;

public interface IMeterImportStore
{
    Task<MeterImportResult> InspectAsync(Guid propertyId, DateOnly period,
        IReadOnlyList<MeterImportRowInput>? rows, CancellationToken ct, bool includeUnoccupied = false);

    Task<MeterImportRow> SaveAsync(Guid propertyId, DateOnly period, OperatorContext actor,
        MeterImportRowInput row, CancellationToken ct);
}
