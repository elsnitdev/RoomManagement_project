using RoomManagement.Domain.Entities;

namespace RoomManagement.Business.Interfaces;

public interface IPropertyStore
{
    /// <summary>Thêm khu trọ mới và lưu vào cơ sở dữ liệu.</summary>
    Task AddAsync(Property property, CancellationToken cancellationToken);
    /// <summary>Tìm khu trọ thuộc chủ trọ theo mã định danh.</summary>
    Task<Property?> GetAsync(Guid propertyId, string ownerId, CancellationToken cancellationToken);
    /// <summary>Lấy danh sách khu trọ của chủ trọ theo bộ lọc và phân trang.</summary>
    Task<(IReadOnlyList<Property> Items, long TotalItems)> ListAsync(string ownerId, string? search, bool? isActive, int skip, int take, CancellationToken cancellationToken);
    /// <summary>Lưu thay đổi khu trọ và xử lý xung đột cập nhật.</summary>
    Task SaveAsync(CancellationToken cancellationToken);
    /// <summary>Tìm dữ liệu liên quan đang cản trở việc xóa khu trọ.</summary>
    Task<string?> GetDeleteBlockerAsync(Guid propertyId, CancellationToken cancellationToken);
    /// <summary>Xóa khu trọ nếu không có dữ liệu liên quan cản trở.</summary>
    Task DeleteAsync(Property property, CancellationToken cancellationToken);
}
