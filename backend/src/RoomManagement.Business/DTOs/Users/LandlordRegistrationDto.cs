namespace RoomManagement.Business.DTOs.Users;

/// <summary>
/// DTO mô tả một yêu cầu đăng ký chủ trọ, dùng cho màn hình xét duyệt của Admin.
/// Chứa cả thông tin tài khoản và thông tin cá nhân để Admin xem xét.
/// </summary>
public sealed class LandlordRegistrationDto
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? Email { get; set; }

    public string? FullName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? CitizenId { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Address { get; set; }

    /// <summary>Trạng thái duyệt hiện tại: Pending / Approved / Rejected.</summary>
    public string ApprovalStatus { get; set; } = string.Empty;

    /// <summary>Lý do từ chối (nếu có).</summary>
    public string? RejectionReason { get; set; }

    /// <summary>Thời điểm gửi yêu cầu đăng ký (UTC).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Thời điểm Admin xử lý yêu cầu (UTC), null nếu chưa xử lý.</summary>
    public DateTime? ReviewedAt { get; set; }
}
