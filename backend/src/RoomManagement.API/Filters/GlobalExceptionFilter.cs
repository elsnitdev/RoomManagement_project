namespace RoomManagement.API.Filters;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RoomManagement.API.Common;
using RoomManagement.Business.Exceptions;

/// <summary>
/// Bộ lọc ngoại lệ toàn cục (Global Exception Filter) tuân thủ kiến trúc ai-feature-workflow-guide.md:
/// Bắt các ngoại lệ nghiệp vụ chưa được xử lý và chuyển thành ProblemDetails tương ứng:
/// - AppValidationException: 400 Bad Request
/// - AppNotFoundException: 404 Not Found
/// - AppConflictException: 409 Conflict
/// - BusinessRuleException: 422 Unprocessable Entity
/// - Unhandled Exception: 500 Internal Server Error
/// Giữ an toàn và tính nhất quán cho toàn bộ endpoint trong hệ thống.
/// </summary>
public sealed class GlobalExceptionFilter : IExceptionFilter
{
    private readonly ILogger<GlobalExceptionFilter> _logger;

    public GlobalExceptionFilter(ILogger<GlobalExceptionFilter> logger)
    {
        _logger = logger;
    }

    public void OnException(ExceptionContext context)
    {
        if (context.ExceptionHandled)
            return;

        var exception = context.Exception;

        switch (exception)
        {
            case BusinessRuleException bizEx:
                _logger.LogWarning(bizEx, "Lỗi vi phạm quy tắc nghiệp vụ: {Message}", bizEx.Message);
                context.Result = new UnprocessableEntityObjectResult(ProblemDetailsHelper.Create(
                    StatusCodes.Status422UnprocessableEntity,
                    "Vi phạm quy tắc nghiệp vụ",
                    bizEx.Message,
                    code: bizEx.Code ?? "BUSINESS_RULE_VIOLATION"));
                context.ExceptionHandled = true;
                break;

            case AppValidationException valEx:
                _logger.LogWarning(valEx, "Lỗi kiểm tra tính hợp lệ dữ liệu đầu vào: {Message}", valEx.Message);
                context.Result = new BadRequestObjectResult(ProblemDetailsHelper.Create(
                    StatusCodes.Status400BadRequest,
                    "Dữ liệu không hợp lệ",
                    valEx.Message,
                    code: valEx.Code ?? "VALIDATION_ERROR"));
                context.ExceptionHandled = true;
                break;

            case AppNotFoundException notFoundEx:
                _logger.LogWarning(notFoundEx, "Không tìm thấy tài nguyên: {Message}", notFoundEx.Message);
                context.Result = new NotFoundObjectResult(ProblemDetailsHelper.Create(
                    StatusCodes.Status404NotFound,
                    "Không tìm thấy tài nguyên",
                    notFoundEx.Message,
                    code: notFoundEx.Code ?? "NOT_FOUND"));
                context.ExceptionHandled = true;
                break;

            case AppConflictException conflictEx:
                _logger.LogWarning(conflictEx, "Xung đột dữ liệu nghiệp vụ: {Message}", conflictEx.Message);
                context.Result = new ConflictObjectResult(ProblemDetailsHelper.Create(
                    StatusCodes.Status409Conflict,
                    "Xung đột dữ liệu",
                    conflictEx.Message,
                    code: conflictEx.Code ?? "DATA_CONFLICT"));
                context.ExceptionHandled = true;
                break;

            case OperationCanceledException:
                _logger.LogInformation("Yêu cầu đã bị hủy bởi phía người dùng (Client Closed Request).");
                context.Result = new StatusCodeResult(499);
                context.ExceptionHandled = true;
                break;

            default:
                _logger.LogError(exception, "Đã xảy ra lỗi không xác định trong hệ thống.");
                context.Result = new ObjectResult(ProblemDetailsHelper.Create(
                    StatusCodes.Status500InternalServerError,
                    "Lỗi máy chủ nội bộ",
                    "Hệ thống gặp sự cố trong quá trình xử lý. Vui lòng thử lại sau.",
                    code: "INTERNAL_SERVER_ERROR"))
                {
                    StatusCode = StatusCodes.Status500InternalServerError
                };
                context.ExceptionHandled = true;
                break;
        }
    }
}
