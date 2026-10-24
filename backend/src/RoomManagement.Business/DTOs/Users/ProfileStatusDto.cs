namespace RoomManagement.Business.DTOs.Users;

/// <summary>
/// DTO cho biết hồ sơ cá nhân của người dùng đã đủ thông tin bắt buộc hay chưa.
/// Client dựa vào đây để quyết định có bắt người dùng điền bổ sung hay không.
/// </summary>
public sealed class ProfileStatusDto
{
    public string UserId { get; set; } = string.Empty;

    /// <summary>true nếu đã điền đủ mọi thông tin bắt buộc.</summary>
    public bool IsProfileCompleted { get; set; }

    /// <summary>
    /// Danh sách tên các trường còn thiếu (ví dụ: "FullName", "CitizenId").
    /// Danh sách rỗng khi hồ sơ đã đủ.
    /// </summary>
    public IReadOnlyList<string> MissingFields { get; set; } = [];
}
