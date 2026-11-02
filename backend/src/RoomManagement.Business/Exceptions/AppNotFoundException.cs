namespace RoomManagement.Business.Exceptions;

/// <summary>
/// Ngoại lệ ném ra khi không tìm thấy tài nguyên được yêu cầu.
/// Tuân thủ quy định tại ai-feature-workflow-guide.md:
/// - Khi bắt gặp ngoại lệ này, tầng API sẽ trả về HTTP Status 404 Not Found
///   dưới dạng ProblemDetails cùng mã lỗi ổn định (Code).
/// </summary>
public class AppNotFoundException : Exception
{
    /// <summary>
    /// Mã lỗi ổn định đại diện cho tài nguyên không tìm thấy (dùng cho ProblemDetails.code).
    /// </summary>
    public string? Code { get; }

    public AppNotFoundException(string message, string? code = null) : base(message)
    {
        Code = code;
    }

    public AppNotFoundException(string message, Exception innerException, string? code = null)
        : base(message, innerException)
    {
        Code = code;
    }
}
