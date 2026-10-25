using RoomManagement.Domain.Entities;

namespace RoomManagement.Business.Interfaces;

/// <summary>
/// Interface mô tả các thao tác truy xuất và lưu dữ liệu cho người dùng (AppUser).
/// Tuân thủ Clean Architecture theo ai-feature-workflow-guide.md:
/// - Interface nằm ở RoomManagement.Business/Interfaces
/// - Implementation nằm ở Data layer
/// - Service chỉ giao tiếp qua interface này, không phụ thuộc trực tiếp vào DbContext hay EF Core.
/// </summary>
public interface IUserStore
{
    /// <summary>
    /// Kiểm tra xem tên đăng nhập (đã chuẩn hóa viết hoa) đã tồn tại trong hệ thống hay chưa.
    /// </summary>
    /// <param name="normalizedUserName">Tên đăng nhập đã qua Normalize.</param>
    /// <param name="cancellationToken">Token hủy thao tác không đồng bộ.</param>
    /// <returns>True nếu đã tồn tại, False nếu chưa.</returns>
    Task<bool> ExistsByNormalizedUserNameAsync(string normalizedUserName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra xem địa chỉ email (đã chuẩn hóa viết hoa) đã tồn tại trong hệ thống hay chưa.
    /// </summary>
    /// <param name="normalizedEmail">Email đã qua Normalize.</param>
    /// <param name="cancellationToken">Token hủy thao tác không đồng bộ.</param>
    /// <returns>True nếu đã tồn tại, False nếu chưa.</returns>
    Task<bool> ExistsByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra xem số CCCD đã được sử dụng bởi người dùng khác hay chưa.
    /// </summary>
    /// <param name="citizenId">Số CCCD cần kiểm tra.</param>
    /// <param name="excludeUserId">ID của người dùng cần loại trừ (khi cập nhật hồ sơ chính mình), null khi tạo mới.</param>
    /// <param name="cancellationToken">Token hủy thao tác không đồng bộ.</param>
    /// <returns>True nếu đã tồn tại số CCCD ở người khác, False nếu hợp lệ.</returns>
    Task<bool> ExistsByCitizenIdAsync(string citizenId, string? excludeUserId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tìm người dùng theo ID với chế độ AsNoTracking (phục vụ xem dữ liệu/tình trạng hồ sơ, tối ưu hiệu năng).
    /// </summary>
    /// <param name="userId">ID người dùng.</param>
    /// <param name="cancellationToken">Token hủy thao tác không đồng bộ.</param>
    /// <returns>Thực thể AppUser hoặc null nếu không tìm thấy.</returns>
    Task<AppUser?> FindByIdAsNoTrackingAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tìm người dùng theo ID có tracking (để cập nhật dữ liệu và lưu lại).
    /// </summary>
    /// <param name="userId">ID người dùng.</param>
    /// <param name="cancellationToken">Token hủy thao tác không đồng bộ.</param>
    /// <returns>Thực thể AppUser được theo dõi hoặc null nếu không tìm thấy.</returns>
    Task<AppUser?> FindByIdAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Thêm người dùng mới vào cơ sở dữ liệu.
    /// </summary>
    /// <param name="user">Thực thể AppUser cần thêm.</param>
    /// <param name="cancellationToken">Token hủy thao tác không đồng bộ.</param>
    Task AddAsync(AppUser user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lưu tất cả thay đổi trên cơ sở dữ liệu.
    /// </summary>
    /// <param name="cancellationToken">Token hủy thao tác không đồng bộ.</param>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
