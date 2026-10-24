using RoomManagement.Domain.Constants;

namespace RoomManagement.Domain.Entities;

public sealed class AppUser
{
    public string Id { get; set; } = null!;
    public string UserName { get; set; } = null!;
    public string NormalizedUserName { get; set; } = null!;
    public string? Email { get; set; }
    public string? NormalizedEmail { get; set; }
    public string PasswordHash { get; set; } = null!;
    public string Role { get; set; } = null!;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }

    // ---------------------------------------------------------------------
    // THÔNG TIN CÁ NHÂN (PROFILE)
    // Tất cả đều cho phép null vì tài khoản do Admin tạo hộ ban đầu chưa có các
    // thông tin này. Người dùng sẽ bị yêu cầu điền bổ sung ở lần đăng nhập đầu.
    // ---------------------------------------------------------------------

    /// <summary>Họ và tên đầy đủ.</summary>
    public string? FullName { get; set; }

    /// <summary>Số điện thoại liên hệ.</summary>
    public string? PhoneNumber { get; set; }

    /// <summary>Số Căn cước công dân (duy nhất trong hệ thống).</summary>
    public string? CitizenId { get; set; }

    /// <summary>Ngày sinh.</summary>
    public DateOnly? DateOfBirth { get; set; }

    /// <summary>Địa chỉ thường trú.</summary>
    public string? Address { get; set; }

    // ---------------------------------------------------------------------
    // THÔNG TIN XÉT DUYỆT
    // ---------------------------------------------------------------------

    /// <summary>
    /// Trạng thái duyệt (xem <see cref="ApprovalStatuses"/>).
    /// Mặc định là Approved để tài khoản do Admin tạo dùng được ngay;
    /// chỉ luồng chủ trọ tự đăng ký mới gán Pending.
    /// </summary>
    public string ApprovalStatus { get; set; } = ApprovalStatuses.Approved;

    /// <summary>Lý do Admin từ chối (chỉ có giá trị khi bị từ chối).</summary>
    public string? RejectionReason { get; set; }

    /// <summary>Thời điểm Admin duyệt hoặc từ chối (giờ UTC).</summary>
    public DateTime? ReviewedAt { get; set; }
}
