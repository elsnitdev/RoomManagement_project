using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RoomManagement.API.Common;
using RoomManagement.Business.DTOs.Users;
using RoomManagement.Business.Interfaces;

namespace RoomManagement.API.Filters;

/// <summary>
/// Chặn tài khoản đang bị buộc đổi mật khẩu tạm (MustChangePassword = true) gọi các API nghiệp vụ.
/// Chỉ cho phép các endpoint [AllowAnonymous] hoặc [AllowMustChangePassword].
/// </summary>
public sealed class MustChangePasswordFilter : IAsyncAuthorizationFilter
{
    private readonly IAuthenticationService _authenticationService;

    public MustChangePasswordFilter(IAuthenticationService authenticationService)
    {
        _authenticationService = authenticationService;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var endpoint = context.HttpContext.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is not null ||
            endpoint?.Metadata.GetMetadata<AllowMustChangePasswordAttribute>() is not null)
        {
            return;
        }

        var principal = context.HttpContext.User;
        if (principal.Identity?.IsAuthenticated != true)
            return;

        var activeUser = context.HttpContext.Items["CurrentActiveUser"] as UserDto;
        if (activeUser is null)
        {
            var userId = principal.FindFirst("sub")?.Value;
            if (!string.IsNullOrWhiteSpace(userId))
                activeUser = await _authenticationService.GetActiveUserAsync(userId, context.HttpContext.RequestAborted);
        }

        if (activeUser is { MustChangePassword: true })
        {
            var problem = ProblemDetailsHelper.Create(
                StatusCodes.Status403Forbidden,
                "Yêu cầu đổi mật khẩu",
                "Bạn phải đổi mật khẩu tạm trước khi sử dụng các chức năng nghiệp vụ.",
                code: "PASSWORD_CHANGE_REQUIRED");
            problem.Instance = context.HttpContext.Request.Path;
            problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

            context.Result = new ObjectResult(problem)
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
    }
}
