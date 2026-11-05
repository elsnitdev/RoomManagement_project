using RoomManagement.Business.DTOs;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Business.Interfaces;

public interface IRoomStore
{
    /// <summary>Thêm phòng mới và lưu vào cơ sở dữ liệu.</summary>
    Task AddAsync(Room room, CancellationToken cancellationToken);
    /// <summary>Tìm phòng thuộc khu trọ của chủ trọ theo mã định danh.</summary>
    Task<Room?> GetAsync(Guid propertyId, Guid roomId, string ownerId, CancellationToken cancellationToken);
    /// <summary>Lấy phòng cùng trạng thái hiệu lực của nó.</summary>
    Task<RoomResult?> GetResultAsync(Guid propertyId, Guid roomId, string ownerId, CancellationToken cancellationToken);
    /// <summary>Lấy danh sách phòng theo bộ lọc và phân trang.</summary>
    Task<(IReadOnlyList<RoomResult> Items, long TotalItems)> ListAsync(Guid propertyId, string ownerId, string? search, RoomStatus? status, Guid? rowId, int? floor, int skip, int take, CancellationToken cancellationToken);
    /// <summary>Kiểm tra số phòng đã tồn tại trong khu trọ hay chưa.</summary>
    Task<bool> NumberExistsAsync(Guid propertyId, string number, Guid? exceptRoomId, CancellationToken cancellationToken);
    /// <summary>Kiểm tra phòng có hợp đồng đang hoạt động hay không.</summary>
    Task<bool> HasActiveContractAsync(Guid roomId, CancellationToken cancellationToken);
    /// <summary>Tìm dữ liệu liên quan đang cản trở việc xóa phòng.</summary>
    Task<string?> GetDeleteBlockerAsync(Guid roomId, CancellationToken cancellationToken);
    /// <summary>Lưu thay đổi phòng và xử lý xung đột cập nhật.</summary>
    Task SaveAsync(CancellationToken cancellationToken);
    /// <summary>Xóa phòng nếu không có dữ liệu liên quan cản trở.</summary>
    Task DeleteAsync(Room room, CancellationToken cancellationToken);
}
