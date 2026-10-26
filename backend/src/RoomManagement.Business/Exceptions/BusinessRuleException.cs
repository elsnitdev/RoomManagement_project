namespace RoomManagement.Business.Exceptions;

/// <summary>
/// Ngoại lệ đại diện cho việc vi phạm quy tắc nghiệp vụ (Business Rule Violation).
/// Tuân thủ quy chuẩn Clean Architecture và tài liệu ai-feature-workflow-guide.md:
/// - Tầng Business kiểm tra logic nghiệp vụ và ném ra BusinessRuleException khi điều kiện nghiệp vụ không thỏa mãn.
/// - Tầng API bắt ngoại lệ này và ánh xạ sang HTTP Status 422 (Unprocessable Entity)
///   theo định dạng chuẩn RFC 7807 ProblemDetails cùng mã lỗi ổn định (Code) để Frontend xử lý.
/// </summary>
public class BusinessRuleException : Exception
{
    /// <summary>
    /// Mã lỗi định danh nghiệp vụ ổn định (ví dụ: "CANNOT_CREATE_ADMIN_ACCOUNT", "INVALID_ROLE").
    /// Giúp Frontend có thể bắt mã lỗi và hiển thị thông báo/điều hướng phù hợp thay vì phụ thuộc vào chuỗi Message.
    /// </summary>
    public string? Code { get; }

    /// <summary>
    /// Khởi tạo ngoại lệ vi phạm quy tắc nghiệp vụ với thông điệp và mã lỗi tùy chọn.
    /// </summary>
    /// <param name="message">Thông điệp chi tiết mô tả vi phạm nghiệp vụ.</param>
    /// <param name="code">Mã lỗi định danh nghiệp vụ ổn định.</param>
    public BusinessRuleException(string message, string? code = null) : base(message)
    {
        Code = code;
    }

    /// <summary>
    /// Khởi tạo ngoại lệ vi phạm quy tắc nghiệp vụ với thông điệp, ngoại lệ gốc và mã lỗi tùy chọn.
    /// </summary>
    public BusinessRuleException(string message, Exception innerException, string? code = null)
        : base(message, innerException)
    {
        Code = code;
    }
}
