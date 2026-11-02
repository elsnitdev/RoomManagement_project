using RoomManagement.Business.DTOs.Tenants;

namespace RoomManagement.Business.Interfaces;

/// <summary>
/// Interface định nghĩa các nghiệp vụ quản lý hồ sơ người thuê dành cho Chủ trọ.
/// Đảm bảo tính độc lập giữa hồ sơ người thuê và tài khoản người dùng đăng nhập.
/// </summary>
public interface ITenantProfileService
{
    /// <summary>
    /// Chủ trọ tạo mới hồ sơ người thuê (chưa có tài khoản đăng nhập).
    /// </summary>
    /// <param name="landlordId">ID của chủ trọ đang thực hiện thao tác.</param>
    /// <param name="dto">Dữ liệu thông tin cá nhân của người thuê.</param>
    /// <param name="ct">Token hủy thao tác không đồng bộ.</param>
    /// <returns>Thông tin hồ sơ người thuê vừa được tạo.</returns>
    Task<TenantProfileDto> CreateProfileAsync(string landlordId, CreateTenantProfileDto dto, CancellationToken ct = default);

    /// <summary>
    /// Chủ trọ xem chi tiết thông tin một hồ sơ người thuê thuộc quyền quản lý.
    /// </summary>
    /// <param name="landlordId">ID của chủ trọ đang thực hiện thao tác.</param>
    /// <param name="id">Mã định danh duy nhất của hồ sơ người thuê.</param>
    /// <param name="ct">Token hủy thao tác không đồng bộ.</param>
    /// <returns>Thông tin chi tiết hồ sơ người thuê.</returns>
    Task<TenantProfileDto> GetProfileByIdAsync(string landlordId, Guid id, CancellationToken ct = default);

    /// <summary>
    /// Chủ trọ xem danh sách phân trang và tìm kiếm hồ sơ người thuê trong phạm vi quản lý.
    /// </summary>
    /// <param name="landlordId">ID của chủ trọ đang thực hiện thao tác.</param>
    /// <param name="query">Tham số tìm kiếm và phân trang.</param>
    /// <param name="ct">Token hủy thao tác không đồng bộ.</param>
    /// <returns>Danh sách hồ sơ người thuê và tổng số lượng bản ghi.</returns>
    Task<(IReadOnlyList<TenantProfileDto> Items, int TotalCount)> GetProfilesAsync(
        string landlordId, TenantProfileQueryDto query, CancellationToken ct = default);

    /// <summary>
    /// Chủ trọ cập nhật thông tin hồ sơ người thuê thuộc quyền quản lý.
    /// </summary>
    /// <param name="landlordId">ID của chủ trọ đang thực hiện thao tác.</param>
    /// <param name="id">Mã định danh duy nhất của hồ sơ người thuê.</param>
    /// <param name="dto">Thông tin cập nhật mới.</param>
    /// <param name="ct">Token hủy thao tác không đồng bộ.</param>
    /// <returns>Thông tin hồ sơ sau khi cập nhật thành công.</returns>
    Task<TenantProfileDto> UpdateProfileAsync(string landlordId, Guid id, UpdateTenantProfileDto dto, CancellationToken ct = default);
}
