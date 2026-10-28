using RoomManagement.Business.DTOs.Tenants;

namespace RoomManagement.API.Contracts.Tenants;

/// <summary>
/// Lớp đại diện cho dữ liệu JSON mà Frontend gửi lên khi yêu cầu cập nhật hồ sơ người thuê (Request Body).
/// </summary>
public sealed class UpdateTenantProfileRequest
{
    /// <summary>Họ và tên người thuê (Bắt buộc, tối đa 150 ký tự).</summary>
    public string FullName { get; init; } = string.Empty;

    /// <summary>Số điện thoại liên hệ (Bắt buộc, 10 chữ số bắt đầu bằng số 0).</summary>
    public string PhoneNumber { get; init; } = string.Empty;

    /// <summary>Địa chỉ email (Tùy chọn, tối đa 150 ký tự).</summary>
    public string? Email { get; init; }

    /// <summary>Số Căn cước công dân / CMND (Tùy chọn, đúng 12 chữ số).</summary>
    public string? CitizenId { get; init; }

    /// <summary>Ngày tháng năm sinh (Tùy chọn, phải trong quá khứ, định dạng JSON: "YYYY-MM-DD").</summary>
    public DateOnly? DateOfBirth { get; init; }

    /// <summary>Giới tính: Male / Female / Other (Tùy chọn).</summary>
    public string? Gender { get; init; }

    /// <summary>Địa chỉ thường trú (Tùy chọn, tối đa 300 ký tự).</summary>
    public string? PermanentAddress { get; init; }

    /// <summary>
    /// Hàm chuyển đổi dữ liệu từ Request (tầng API) sang DTO (tầng Business) để truyền vào Service xử lý.
    /// </summary>
    public UpdateTenantProfileDto ToDto() => new()
    {
        FullName = FullName,
        PhoneNumber = PhoneNumber,
        Email = Email,
        CitizenId = CitizenId,
        DateOfBirth = DateOfBirth,
        Gender = Gender,
        PermanentAddress = PermanentAddress
    };
}
