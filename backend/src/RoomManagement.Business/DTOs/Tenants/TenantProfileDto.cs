namespace RoomManagement.Business.DTOs.Tenants;

/// <summary>
/// DTO chứa thông tin chi tiết đầy đủ của hồ sơ người thuê trả về cho tầng API/Client.
/// </summary>
public sealed class TenantProfileDto
{
    /// <summary>Mã định danh duy nhất của hồ sơ người thuê.</summary>
    public Guid Id { get; set; }

    /// <summary>ID tài khoản người dùng đăng nhập nếu đã liên kết (null nếu chưa có tài khoản).</summary>
    public string? UserId { get; set; }

    /// <summary>ID chủ trọ khởi tạo hồ sơ.</summary>
    public string? CreatedByUserId { get; set; }

    /// <summary>Họ và tên người thuê.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Số điện thoại liên hệ.</summary>
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>Địa chỉ email.</summary>
    public string? Email { get; set; }

    /// <summary>Số Căn cước công dân / CMND.</summary>
    public string? CitizenId { get; set; }

    /// <summary>Ngày tháng năm sinh.</summary>
    public DateOnly? DateOfBirth { get; set; }

    /// <summary>Giới tính: Male / Female / Other.</summary>
    public string? Gender { get; set; }

    /// <summary>Địa chỉ thường trú.</summary>
    public string? PermanentAddress { get; set; }

    /// <summary>Thời điểm tạo hồ sơ (UTC).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Thời điểm cập nhật hồ sơ gần nhất (UTC).</summary>
    public DateTime? UpdatedAt { get; set; }
}
