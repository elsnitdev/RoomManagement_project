namespace RoomManagement.Domain.Entities;

/// <summary>
/// Thực thể (Entity) đại diện cho bảng dbo.TenantProfiles trong cơ sở dữ liệu.
/// Lưu trữ thông tin nhân khẩu của người thuê độc lập với tài khoản đăng nhập (AppUser).
/// </summary>
public sealed class TenantProfile // từ khóa sealed: ngăn không cho class khác kế thừa class này
{
    /// <summary>Khóa chính (Primary Key) kiểu Guid (chuỗi định danh duy nhất toàn cục 128-bit).</summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Khóa ngoại trỏ tới bảng Users (AppUser.Id).
    /// Dấu '?' (Nullable) cho biết trường này có thể mang giá trị null khi người thuê chưa có tài khoản đăng nhập.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// ID của Chủ trọ đã tạo ra hồ sơ này. Dùng để phân quyền: chủ trọ nào tạo thì chủ trọ đó được xem/sửa.
    /// </summary>
    public string? CreatedByUserId { get; set; }

    /// <summary>
    /// Họ và tên người thuê (Bắt buộc).
    /// '= null!' báo cho trình biên dịch C# biết giá trị sẽ được gán trước khi sử dụng (tắt cảnh báo null).
    /// </summary>
    public string FullName { get; set; } = null!;

    /// <summary>Số điện thoại liên hệ của người thuê (Bắt buộc).</summary>
    public string PhoneNumber { get; set; } = null!;

    /// <summary>Địa chỉ email (Tùy chọn, có thể null).</summary>
    public string? Email { get; set; }

    /// <summary>Số Căn cước công dân / CMND 12 số (Tùy chọn, có thể null nhưng không được trùng nếu có).</summary>
    public string? CitizenId { get; set; }

    /// <summary>
    /// Ngày tháng năm sinh (Tùy chọn).
    /// Kiểu DateOnly trong .NET chỉ lưu Ngày-Tháng-Năm (không lưu Giờ-Phút-Giây), tương ứng với kiểu 'date' trong SQL.
    /// </summary>
    public DateOnly? DateOfBirth { get; set; }

    /// <summary>Giới tính: "Male", "Female" hoặc "Other" (Tùy chọn).</summary>
    public string? Gender { get; set; }

    /// <summary>Địa chỉ thường trú trên giấy tờ (Tùy chọn).</summary>
    public string? PermanentAddress { get; set; }

    /// <summary>Thời điểm hồ sơ được tạo (lưu theo giờ chuẩn quốc tế UTC).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Thời điểm cập nhật gần nhất (null nếu chưa từng chỉnh sửa sau khi tạo).</summary>
    public DateTime? UpdatedAt { get; set; }
}
