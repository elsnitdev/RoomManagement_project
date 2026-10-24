using RoomManagement.Business.DTOs.Users;

namespace RoomManagement.API.Contracts.Users;

/// <summary>
/// Kết quả kiểm tra hồ sơ cá nhân trả về cho Client.
/// Nếu IsProfileCompleted = false, Client phải đưa người dùng tới màn hình điền thông tin.
/// </summary>
public sealed class ProfileStatusResponse
{
    public string UserId { get; init; } = string.Empty;

    /// <summary>true nếu đã đủ thông tin bắt buộc.</summary>
    public bool IsProfileCompleted { get; init; }

    /// <summary>Tên các trường còn thiếu, ví dụ: ["FullName", "CitizenId"].</summary>
    public IReadOnlyList<string> MissingFields { get; init; } = [];

    /// <summary>
    /// Chuyển từ DTO tầng Business sang dữ liệu trả về cho Client.
    /// </summary>
    public static ProfileStatusResponse FromDto(ProfileStatusDto dto) => new()
    {
        UserId = dto.UserId,
        IsProfileCompleted = dto.IsProfileCompleted,
        MissingFields = dto.MissingFields
    };
}
