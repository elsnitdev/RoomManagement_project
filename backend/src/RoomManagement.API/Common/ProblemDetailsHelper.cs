namespace RoomManagement.API.Common;

using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Lớp tiện ích hỗ trợ sinh đối tượng ProblemDetails chuẩn RFC 7807 theo đúng quy định tại ai-feature-workflow-guide.md:
/// - Lỗi validation dữ liệu đầu vào: 400 Bad Request
/// - Lỗi không tìm thấy: 404 Not Found
/// - Lỗi trùng dữ liệu / xung đột cạnh tranh: 409 Conflict
/// - Lỗi vi phạm quy tắc nghiệp vụ: 422 Unprocessable Entity
/// - Lỗi máy chủ nội bộ: 500 Internal Server Error
/// Bổ sung trường code định danh ổn định để frontend dễ dàng xử lý.
/// </summary>
public static class ProblemDetailsHelper
{
    /// <summary>
    /// Tạo đối tượng ProblemDetails chuẩn hóa kèm mã code ổn định và trường message tương thích.
    /// </summary>
    /// <param name="statusCode">Mã trạng thái HTTP (400, 404, 409, 422, 500...).</param>
    /// <param name="title">Tiêu đề nhóm lỗi ngắn gọn.</param>
    /// <param name="detail">Nội dung chi tiết của lỗi.</param>
    /// <param name="code">Mã định danh lỗi ổn định (ví dụ: "VALIDATION_FAILED", "CANNOT_CREATE_ADMIN_ACCOUNT").</param>
    /// <param name="errors">Danh sách lỗi chi tiết cho từng trường nếu có.</param>
    /// <returns>Đối tượng ProblemDetails đã được chuẩn hóa.</returns>
    public static ProblemDetails Create(
        int statusCode,
        string title,
        string detail,
        string? code = null,
        IDictionary<string, string[]>? errors = null)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail
        };

        if (!string.IsNullOrWhiteSpace(code))
        {
            problem.Extensions["code"] = code;
        }

        // Bổ sung trường message trong extensions để tương thích ngược 100% với các client đọc data.message
        problem.Extensions["message"] = detail;

        if (errors != null && errors.Count > 0)
        {
            problem.Extensions["errors"] = errors;
        }

        return problem;
    }
}
