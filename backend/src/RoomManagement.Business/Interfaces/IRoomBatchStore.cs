using RoomManagement.Business.DTOs;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Business.Interfaces;

public interface IRoomBatchStore
{
    Task<IReadOnlyList<RoomRow>> ListRowsAsync(Guid propertyId, CancellationToken ct);
    Task<RoomRow> AddRowAsync(Guid propertyId, string code, string name, CancellationToken ct);
    Task<RoomRow> UpdateRowAsync(Guid propertyId, Guid rowId, string name, bool isActive, CancellationToken ct);
    Task<RoomBatchPreviewResult> PreviewAsync(Guid propertyId, RoomBatchPlan plan, CancellationToken ct);
    Task<RoomBatchCreateResult> CreateAsync(Guid propertyId, string ownerId, RoomBatchPlan plan, string fingerprint, Guid idempotencyKey, CancellationToken ct);
}
