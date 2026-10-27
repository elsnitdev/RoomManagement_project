using RoomManagement.Business.DTOs.Tenants;

namespace RoomManagement.API.Contracts.Tenants;

/// <summary>
/// Dữ liệu phản hồi thông tin hồ sơ người thuê gửi về cho Client.
/// </summary>
public sealed class TenantProfileResponse
{
    /// <summary>Mã định danh duy nhất của hồ sơ người thuê.</summary>
    public Guid Id { get; init; }

    /// <summary>ID tài khoản người dùng nếu đã liên kết tài khoản.</summary>
    public string? UserId { get; init; }

    /// <summary>ID của chủ trọ khởi tạo hồ sơ.</summary>
    public string? CreatedByUserId { get; init; }

    /// <summary>Họ và tên người thuê.</summary>
    public string FullName { get; init; } = string.Empty;

    /// <summary>Số điện thoại liên hệ.</summary>
    public string PhoneNumber { get; init; } = string.Empty;

    /// <summary>Địa chỉ email.</summary>
    public string? Email { get; init; }

    /// <summary>Số Căn cước công dân / CMND.</summary>
    public string? CitizenId { get; init; }

    /// <summary>Ngày tháng năm sinh.</summary>
    public DateOnly? DateOfBirth { get; init; }

    /// <summary>Giới tính: Male / Female / Other.</summary>
    public string? Gender { get; init; }

    /// <summary>Địa chỉ thường trú.</summary>
    public string? PermanentAddress { get; init; }

    /// <summary>Ngày giờ khởi tạo hồ sơ (UTC).</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>Ngày giờ cập nhật hồ sơ gần nhất (UTC).</summary>
    public DateTime? UpdatedAt { get; init; }

    /// <summary>
    /// Chuyển đổi từ Business DTO sang Response Contract.
    /// </summary>
    public static TenantProfileResponse FromDto(TenantProfileDto dto) => new()
    {
        Id = dto.Id,
        UserId = dto.UserId,
        CreatedByUserId = dto.CreatedByUserId,
        FullName = dto.FullName,
        PhoneNumber = dto.PhoneNumber,
        Email = dto.Email,
        CitizenId = dto.CitizenId,
        DateOfBirth = dto.DateOfBirth,
        Gender = dto.Gender,
        PermanentAddress = dto.PermanentAddress,
        CreatedAt = dto.CreatedAt,
        UpdatedAt = dto.UpdatedAt
    };
}
