using Microsoft.AspNetCore.Mvc;
using RoomManagement.API.Contracts.Auth;

namespace RoomManagement.API.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        // TODO: xác thực tài khoản và mật khẩu, sau đó cấp phiên đăng nhập.
        return StatusCode(StatusCodes.Status501NotImplemented, new
        {
            message = "API đăng nhập đã nhận yêu cầu; chức năng xác thực chưa được triển khai."
        });
    }

    [HttpPost("register")]
    public IActionResult Register([FromBody] RegisterRequest request)
    {
        // TODO: kiểm tra trùng tài khoản, băm mật khẩu và tạo người dùng với quyền phù hợp.
        return StatusCode(StatusCodes.Status501NotImplemented, new
        {
            message = "API đăng ký đã nhận yêu cầu; chức năng tạo tài khoản chưa được triển khai."
        });
    }
}
