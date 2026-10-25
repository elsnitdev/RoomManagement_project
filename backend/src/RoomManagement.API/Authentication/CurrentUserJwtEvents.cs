using Microsoft.AspNetCore.Authentication.JwtBearer;
using RoomManagement.Business.Interfaces;

namespace RoomManagement.API.Authentication;

public sealed class CurrentUserJwtEvents(IAuthenticationService authenticationService) : JwtBearerEvents
{
    public override async Task TokenValidated(TokenValidatedContext context)
    {
        var userId = context.Principal?.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(userId))
        {
            context.Fail("Token không có danh tính hợp lệ.");
            return;
        }

        var user = await authenticationService.GetActiveUserAsync(userId, context.HttpContext.RequestAborted);
        if (user is null || user.Role != context.Principal?.FindFirst("role")?.Value)
            context.Fail("Tài khoản không còn quyền truy cập. Vui lòng đăng nhập lại.");
    }
}
