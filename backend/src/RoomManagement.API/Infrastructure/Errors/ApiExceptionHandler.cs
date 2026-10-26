using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RoomManagement.Business.Errors;

namespace RoomManagement.API.Infrastructure.Errors;

public sealed class ApiExceptionHandler : IExceptionHandler
{
  private readonly ILogger<ApiExceptionHandler> _logger;

  public ApiExceptionHandler(ILogger<ApiExceptionHandler> logger)
  {
    _logger = logger;
  }

  public async ValueTask<bool> TryHandleAsync(
      HttpContext httpContext,
      Exception exception,
      CancellationToken cancellationToken)
  {
    var (status, title, code) = exception switch
    {
      BusinessException { Type: BusinessErrorType.NotFound } business =>
          (StatusCodes.Status404NotFound, "Không tìm thấy dữ liệu", business.Code),
      BusinessException { Type: BusinessErrorType.Conflict } business =>
          (StatusCodes.Status409Conflict, "Dữ liệu bị xung đột", business.Code),
      BusinessException { Type: BusinessErrorType.InvalidOperation } business =>
          (StatusCodes.Status422UnprocessableEntity, "Không thể thực hiện thao tác", business.Code),
      BusinessException { Type: BusinessErrorType.Validation } business =>
          (StatusCodes.Status400BadRequest, "Dữ liệu yêu cầu không hợp lệ", business.Code),
      _ => (StatusCodes.Status500InternalServerError, "Lỗi hệ thống", "INTERNAL_ERROR")
    };

    if (status == StatusCodes.Status500InternalServerError)
    {
      _logger.LogError(exception, "Unhandled request error. TraceId: {TraceId}", httpContext.TraceIdentifier);
    }
    else
    {
      _logger.LogInformation(exception, "Business request error {Code}. TraceId: {TraceId}", code, httpContext.TraceIdentifier);
    }

    httpContext.Response.StatusCode = status;
    var problem = new ProblemDetails
    {
      Status = status,
      Title = title,
      Detail = exception is BusinessException ? exception.Message : "Đã xảy ra lỗi. Vui lòng thử lại sau.",
      Instance = httpContext.Request.Path
    };
    problem.Extensions["code"] = code;
    problem.Extensions["traceId"] = httpContext.TraceIdentifier;

    httpContext.Response.ContentType = "application/problem+json";
    await JsonSerializer.SerializeAsync(
        httpContext.Response.Body,
        problem,
        new JsonSerializerOptions(JsonSerializerDefaults.Web),
        cancellationToken);

    return true;
  }
}
