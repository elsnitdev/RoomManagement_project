namespace RoomManagement.Business.Exceptions;

/// <summary>
/// Ngoại lệ ném ra khi dữ liệu đầu vào không hợp lệ với quy tắc kiểm tra cơ bản.
/// Tuân thủ quy định tại ai-feature-workflow-guide.md:
/// - Khi bắt gặp ngoại lệ này, tầng API sẽ trả về HTTP Status 400 Bad Request
///   dưới dạng ProblemDetails cùng mã lỗi ổn định (Code).
/// </summary>
public class AppValidationException : Exception
{
    /// <summary>
    /// Mã lỗi ổn định đại diện cho loại lỗi validation (dùng cho ProblemDetails.code).
    /// </summary>
    public string? Code { get; }

    public AppValidationException(string message, string? code = null) : base(message)
    {
        Code = code;
    }

    public AppValidationException(string message, Exception innerException, string? code = null)
        : base(message, innerException)
    {
        Code = code;
    }
}
