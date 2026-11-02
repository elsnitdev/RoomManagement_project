namespace RoomManagement.Domain.Constants;

/// <summary>
/// Lớp tĩnh (static class) chứa các hằng số giới tính hợp lệ của hồ sơ người thuê.
/// Static class không cần khởi tạo bằng từ khóa 'new', có thể gọi trực tiếp như: TenantGenders.Male
/// </summary>
public static class TenantGenders
{
    // Các hằng số (const) cố định giá trị chuỗi lưu xuống database
    public const string Male = "Male";
    public const string Female = "Female";
    public const string Other = "Other";

    /// <summary>
    /// Tập hợp chỉ đọc (IReadOnlySet) chứa tất cả giới tính hợp lệ.
    /// Sử dụng HashSet với StringComparer.OrdinalIgnoreCase để kiểm tra tồn tại (.Contains) cực nhanh
    /// và không phân biệt chữ hoa/chữ thường (ví dụ: "male", "MALE", "Male" đều hợp lệ).
    /// </summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Male, Female, Other
    };
}
