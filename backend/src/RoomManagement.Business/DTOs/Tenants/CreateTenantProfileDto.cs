namespace RoomManagement.Business.DTOs.Tenants;

/// <summary>
/// DTO chứa thông tin yêu cầu tạo hồ sơ người thuê mới từ Chủ trọ.
/// Người thuê có thể chưa có tài khoản trong hệ thống.
/// </summary>
public sealed class CreateTenantProfileDto
{
    /// <summary>Họ và tên người thuê (Bắt buộc, tối đa 150 ký tự).</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Số điện thoại liên hệ (Bắt buộc, 10 chữ số bắt đầu bằng số 0).</summary>
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>Địa chỉ email (Tùy chọn, tối đa 150 ký tự).</summary>
    public string? Email { get; set; }

    /// <summary>Số Căn cước công dân / CMND (Tùy chọn, đúng 12 chữ số).</summary>
    public string? CitizenId { get; set; }

    /// <summary>Ngày tháng năm sinh (Tùy chọn, phải trong quá khứ).</summary>
    public DateOnly? DateOfBirth { get; set; }

    /// <summary>Giới tính: Male / Female / Other (Tùy chọn).</summary>
    public string? Gender { get; set; }

    /// <summary>Địa chỉ thường trú (Tùy chọn, tối đa 300 ký tự).</summary>
    public string? PermanentAddress { get; set; }
}
