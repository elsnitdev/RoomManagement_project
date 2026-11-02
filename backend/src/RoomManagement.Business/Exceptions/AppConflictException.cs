namespace RoomManagement.Business.Exceptions;

/// <summary>
/// Ngoại lệ ném ra khi tài nguyên bị xung đột dữ liệu trong hệ thống (trùng dữ liệu duy nhất, xung đột đồng thời).
/// Tuân thủ quy định tại ai-feature-workflow-guide.md:
/// - Khi bắt gặp ngoại lệ này, tầng API sẽ trả về HTTP Status 409 Conflict
///   dưới dạng ProblemDetails cùng mã lỗi ổn định (Code).
/// </summary>
public class AppConflictException : Exception
{
    /// <summary>
    /// Mã lỗi ổn định đại diện cho loại xung đột dữ liệu (dùng cho ProblemDetails.code).
    /// </summary>
    public string? Code { get; }

    public AppConflictException(string message, string? code = null) : base(message)
    {
        Code = code;
    }

    public AppConflictException(string message, Exception innerException, string? code = null)
        : base(message, innerException)
    {
        Code = code;
    }
}
