using RoomManagement.Business.DTOs.Users;

namespace RoomManagement.API.Contracts.Users;

/// <summary>
/// Dữ liệu phản hồi gửi về cho Client sau khi tài khoản được tạo thành công.
/// Không bao gồm mật khẩu hay các thông tin bảo mật nhạy cảm.
/// </summary>
public sealed class UserResponse
{
    /// <summary>
    /// ID định danh duy nhất của người dùng.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Tên đăng nhập.
    /// </summary>
    public string UserName { get; init; } = string.Empty;

    /// <summary>
    /// Email của người dùng.
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// Vai trò đã được cấp (Landlord hoặc Tenant).
    /// </summary>
    public string Role { get; init; } = string.Empty;

    /// <summary>
    /// Trạng thái hoạt động của tài khoản.
    /// </summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// Trạng thái xét duyệt: Pending (chờ duyệt) / Approved (đã duyệt) / Rejected (bị từ chối).
    /// </summary>
    public string ApprovalStatus { get; init; } = string.Empty;

    /// <summary>
    /// Ngày giờ tạo tài khoản (UTC).
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Chuyển từ DTO tầng Business sang dữ liệu trả về cho Client.
    /// </summary>
    public static UserResponse FromDto(UserDto dto) => new()
    {
        Id = dto.Id,
        UserName = dto.UserName,
        Email = dto.Email,
        Role = dto.Role,
        IsActive = dto.IsActive,
        ApprovalStatus = dto.ApprovalStatus,
        CreatedAt = dto.CreatedAt
    };
}
