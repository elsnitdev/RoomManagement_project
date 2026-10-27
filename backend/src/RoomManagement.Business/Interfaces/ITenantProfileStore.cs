using RoomManagement.Domain.Entities;

namespace RoomManagement.Business.Interfaces;

/// <summary>
/// Interface mô tả các thao tác truy xuất và lưu dữ liệu cho hồ sơ người thuê (TenantProfile).
/// Tuân thủ Clean Architecture: Service ở Business chỉ phụ thuộc interface này,
/// không phụ thuộc trực tiếp vào DbContext hay EF Core.
/// </summary>
public interface ITenantProfileStore
{
    /// <summary>
    /// Tìm kiếm hồ sơ người thuê theo ID có tracking để phục vụ cập nhật thông tin.
    /// </summary>
    /// <param name="id">Mã định danh (Guid) của hồ sơ người thuê.</param>
    /// <param name="ct">Token hủy thao tác không đồng bộ.</param>
    /// <returns>Thực thể TenantProfile được theo dõi hoặc null nếu không tìm thấy.</returns>
    Task<TenantProfile?> FindByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Tìm kiếm hồ sơ người thuê theo ID không tracking (tối ưu hiệu năng đọc).
    /// </summary>
    /// <param name="id">Mã định danh (Guid) của hồ sơ người thuê.</param>
    /// <param name="ct">Token hủy thao tác không đồng bộ.</param>
    /// <returns>Thực thể TenantProfile hoặc null nếu không tìm thấy.</returns>
    Task<TenantProfile?> FindByIdAsNoTrackingAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Kiểm tra số CCCD/CMND đã tồn tại trong hệ thống hay chưa (chống trùng lặp CCCD toàn hệ thống).
    /// </summary>
    /// <param name="citizenId">Số CCCD cần kiểm tra.</param>
    /// <param name="excludeId">ID của hồ sơ cần loại trừ (khi cập nhật), null khi tạo mới.</param>
    /// <param name="ct">Token hủy thao tác không đồng bộ.</param>
    /// <returns>True nếu đã tồn tại ở hồ sơ khác, False nếu hợp lệ.</returns>
    Task<bool> ExistsByCitizenIdAsync(string citizenId, Guid? excludeId = null, CancellationToken ct = default);

    /// <summary>
    /// Kiểm tra quyền truy cập của chủ trọ đối với hồ sơ người thuê:
    /// 1. Do chính chủ trọ tạo (CreatedByUserId == landlordId), HOẶC
    /// 2. Đã có hợp đồng thuê tại phòng thuộc khu trọ do chủ trọ sở hữu.
    /// </summary>
    /// <param name="landlordId">ID tài khoản chủ trọ.</param>
    /// <param name="tenantProfileId">Mã định danh của hồ sơ người thuê.</param>
    /// <param name="ct">Token hủy thao tác không đồng bộ.</param>
    /// <returns>True nếu chủ trọ có quyền truy cập, False nếu không.</returns>
    Task<bool> HasAccessPermissionAsync(string landlordId, Guid tenantProfileId, CancellationToken ct = default);

    /// <summary>
    /// Lấy danh sách hồ sơ người thuê thuộc quyền quản lý của chủ trọ có phân trang và tìm kiếm theo từ khóa.
    /// </summary>
    /// <param name="landlordId">ID tài khoản chủ trọ.</param>
    /// <param name="keyword">Từ khóa tìm kiếm (họ tên hoặc số điện thoại).</param>
    /// <param name="pageIndex">Chỉ số trang (bắt đầu từ 1).</param>
    /// <param name="pageSize">Số lượng bản ghi trên một trang.</param>
    /// <param name="ct">Token hủy thao tác không đồng bộ.</param>
    /// <returns>Danh sách hồ sơ và tổng số bản ghi thỏa mãn điều kiện.</returns>
    Task<(IReadOnlyList<TenantProfile> Items, int TotalCount)> GetByLandlordAsync(
        string landlordId, string? keyword, int pageIndex, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// Thêm hồ sơ người thuê mới vào cơ sở dữ liệu.
    /// </summary>
    /// <param name="profile">Thực thể TenantProfile cần thêm.</param>
    /// <param name="ct">Token hủy thao tác không đồng bộ.</param>
    Task AddAsync(TenantProfile profile, CancellationToken ct = default);

    /// <summary>
    /// Lưu các thay đổi vào cơ sở dữ liệu.
    /// Chuyển đổi vi phạm Unique Constraint trên CitizenId thành AppConflictException.
    /// </summary>
    /// <param name="ct">Token hủy thao tác không đồng bộ.</param>
    Task SaveChangesAsync(CancellationToken ct = default);
}
