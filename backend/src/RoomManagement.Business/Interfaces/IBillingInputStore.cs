using RoomManagement.Business.DTOs;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Business.Interfaces;

public interface IBillingInputStore
{
    /// <summary>Kiểm tra khu trọ đang hoạt động có thuộc chủ trọ hay không.</summary>
    Task<bool> PropertyOwnedAsync(Guid propertyId, string ownerId, CancellationToken ct);
    /// <summary>Kiểm tra phòng có thuộc khu trọ đang hoạt động của chủ trọ hay không.</summary>
    Task<bool> RoomOwnedAsync(Guid propertyId, Guid roomId, string ownerId, CancellationToken ct);
    /// <summary>Lấy danh sách đơn giá điện nước của khu trọ, có thể lọc theo loại.</summary>
    Task<IReadOnlyList<UtilityRate>> ListRatesAsync(Guid propertyId, UtilityType? type, CancellationToken ct);
    /// <summary>Tìm đơn giá điện hoặc nước có hiệu lực tại ngày chỉ định.</summary>
    Task<UtilityRate?> RateAtAsync(Guid propertyId, UtilityType type, DateOnly date, CancellationToken ct);
    /// <summary>Lưu đơn giá mới nếu thời gian áp dụng không chồng lấn.</summary>
    Task AddRateAsync(UtilityRate rate, CancellationToken ct);
    /// <summary>Lấy lịch sử chỉ số điện nước của phòng, mới nhất trước.</summary>
    Task<IReadOnlyList<MeterReading>> ListReadingsAsync(Guid roomId, UtilityType? type, CancellationToken ct);
    /// <summary>Tìm chỉ số điện hoặc nước của phòng trong tháng chỉ định.</summary>
    Task<MeterReading?> ReadingAtAsync(Guid roomId, UtilityType type, short year, byte month, CancellationToken ct);
    /// <summary>Tìm kỳ ghi chỉ số gần nhất trước tháng chỉ định.</summary>
    Task<MeterReading?> LatestReadingBeforeAsync(Guid roomId, UtilityType type, short year, byte month, CancellationToken ct);
    /// <summary>Tìm kỳ ghi chỉ số gần nhất sau tháng chỉ định.</summary>
    Task<MeterReading?> NextReadingAfterAsync(Guid roomId, UtilityType type, short year, byte month, CancellationToken ct);
    /// <summary>Lưu chỉ số mới nếu kỳ và chỉ số nối tiếp các kỳ liền kề.</summary>
    Task AddReadingAsync(MeterReading reading, CancellationToken ct);
    /// <summary>Kiểm tra người dùng có phải quản trị viên đang hoạt động hay không.</summary>
    Task<bool> ActiveAdminAsync(string userId, CancellationToken ct);
    /// <summary>Lấy danh mục dịch vụ, sắp xếp theo tên.</summary>
    Task<IReadOnlyList<Service>> ListServicesAsync(CancellationToken ct);
    /// <summary>Tìm dịch vụ theo mã định danh.</summary>
    Task<Service?> GetServiceAsync(Guid serviceId, CancellationToken ct);
    /// <summary>Thêm dịch vụ mới vào danh mục.</summary>
    Task AddServiceAsync(Service service, CancellationToken ct);
    /// <summary>Lấy các dịch vụ đã gán cho phòng cùng thông tin dịch vụ.</summary>
    Task<IReadOnlyList<RoomServiceResult>> ListRoomServicesAsync(Guid roomId, CancellationToken ct);
    /// <summary>Gán dịch vụ cho phòng nếu thời gian áp dụng không chồng lấn.</summary>
    Task AddRoomServiceAsync(RoomService assignment, CancellationToken ct);
}
