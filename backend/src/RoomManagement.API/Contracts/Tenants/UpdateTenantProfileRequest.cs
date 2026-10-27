using RoomManagement.Business.DTOs.Tenants;

namespace RoomManagement.API.Contracts.Tenants;

/// <summary>
/// Yêu cầu cập nhật hồ sơ người thuê từ Chủ trọ.
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

    /// <summary>Ngày tháng năm sinh (Tùy chọn, phải trong quá khứ).</summary>
    public DateOnly? DateOfBirth { get; init; }

    /// <summary>Giới tính: Male / Female / Other (Tùy chọn).</summary>
    public string? Gender { get; init; }

    /// <summary>Địa chỉ thường trú (Tùy chọn, tối đa 300 ký tự).</summary>
    public string? PermanentAddress { get; init; }

    /// <summary>Chuyển đổi sang Business DTO.</summary>
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
