namespace RoomManagement.Domain.Constants;

/// <summary>
/// Các trạng thái xét duyệt của một tài khoản.
/// Được lưu dưới dạng chuỗi trong cột Users.ApprovalStatus (giống cách lưu UserRoles).
/// </summary>
public static class ApprovalStatuses
{
    /// <summary>
    /// Chờ duyệt: chủ trọ vừa tự đăng ký, Admin chưa xem xét.
    /// Tài khoản ở trạng thái này CHƯA được phép quản lý khu trọ.
    /// </summary>
    public const string Pending = "Pending";

    /// <summary>
    /// Đã duyệt: tài khoản được phép sử dụng đầy đủ quyền theo vai trò.
    /// Tài khoản do Admin tạo hoặc tài khoản cũ mặc định ở trạng thái này.
    /// </summary>
    public const string Approved = "Approved";

    /// <summary>
    /// Bị từ chối: Admin không chấp nhận yêu cầu, lý do được lưu ở Users.RejectionReason.
    /// </summary>
    public const string Rejected = "Rejected";
}
