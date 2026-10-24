using RoomManagement.Business.DTOs.Users;

namespace RoomManagement.Business.Interfaces;

/// <summary>
/// Giao diện định nghĩa các nghiệp vụ Admin xét duyệt yêu cầu đăng ký chủ trọ.
/// </summary>
public interface ILandlordApprovalService
{
    /// <summary>
    /// Lấy danh sách các yêu cầu đăng ký chủ trọ đang chờ duyệt (cũ nhất lên đầu).
    /// </summary>
    Task<IReadOnlyList<LandlordRegistrationDto>> GetPendingRegistrationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Duyệt yêu cầu: tài khoản chính thức có quyền Landlord và được kích hoạt.
    /// </summary>
    /// <param name="userId">Id tài khoản chủ trọ cần duyệt.</param>
    Task<LandlordRegistrationDto> ApproveAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Từ chối yêu cầu: bắt buộc kèm lý do để người đăng ký biết cần bổ sung gì.
    /// </summary>
    /// <param name="userId">Id tài khoản chủ trọ cần từ chối.</param>
    /// <param name="reason">Lý do từ chối.</param>
    Task<LandlordRegistrationDto> RejectAsync(string userId, string reason, CancellationToken cancellationToken = default);
}
