using RoomManagement.Domain.Entities;

namespace RoomManagement.Business.Interfaces;

/// <summary>
/// Interface mô tả các thao tác truy xuất và lưu dữ liệu cho quy trình xét duyệt chủ trọ.
/// Tuân thủ Clean Architecture: Service ở Business chỉ phụ thuộc interface này, 
/// không phụ thuộc trực tiếp vào DbContext hay EF Core.
/// </summary>
public interface ILandlordApprovalStore
{
    /// <summary>
    /// Lấy danh sách các tài khoản chủ trọ (Role = Landlord) đang ở trạng thái Chờ duyệt (Pending),
    /// sắp xếp theo thời gian đăng ký sớm nhất lên đầu.
    /// </summary>
    /// <param name="cancellationToken">Token hủy thao tác không đồng bộ.</param>
    /// <returns>Danh sách tài khoản chủ trọ đang chờ duyệt.</returns>
    Task<IReadOnlyList<AppUser>> GetPendingLandlordsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Tìm kiếm tài khoản chủ trọ (Role = Landlord) theo ID người dùng để phục vụ cập nhật trạng thái.
    /// Trả về dữ liệu không theo dõi thay đổi để Business quyết định kết quả duyệt.
    /// </summary>
    /// <param name="userId">Khóa chính (Id) của người dùng.</param>
    /// <param name="cancellationToken">Token hủy thao tác không đồng bộ.</param>
    /// <returns>Tài khoản chủ trọ tìm thấy hoặc null nếu không tồn tại.</returns>
    Task<AppUser?> FindLandlordByIdAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Chỉ cập nhật các trường xét duyệt nếu trạng thái lưu trữ vẫn là Pending.
    /// Trả về false nếu một request khác đã xử lý tài khoản.
    /// </summary>
    /// <param name="cancellationToken">Token hủy thao tác không đồng bộ.</param>
    Task<bool> TryReviewPendingAsync(AppUser reviewedUser, CancellationToken cancellationToken = default);
}
