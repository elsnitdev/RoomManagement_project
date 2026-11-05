using RoomManagement.Business.DTOs;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Business.Interfaces;

public interface IRoomBatchStore
{
    /// <summary>Lấy danh sách dãy phòng của khu trọ.</summary>
    Task<IReadOnlyList<RoomRow>> ListRowsAsync(Guid propertyId, CancellationToken ct);
    /// <summary>Thêm dãy phòng mới vào khu trọ.</summary>
    Task<RoomRow> AddRowAsync(Guid propertyId, string code, string name, CancellationToken ct);
    /// <summary>Cập nhật tên và trạng thái của dãy phòng.</summary>
    Task<RoomRow> UpdateRowAsync(Guid propertyId, Guid rowId, string name, bool isActive, CancellationToken ct);
    /// <summary>Xem trước số phòng và thông tin sẽ được tạo theo kế hoạch.</summary>
    Task<RoomBatchPreviewResult> PreviewAsync(Guid propertyId, RoomBatchPlan plan, CancellationToken ct);
    /// <summary>Tạo nhiều phòng theo bản xem trước và khóa chống gửi trùng.</summary>
    Task<RoomBatchCreateResult> CreateAsync(Guid propertyId, string ownerId, RoomBatchPlan plan, string fingerprint, Guid idempotencyKey, CancellationToken ct);
}
