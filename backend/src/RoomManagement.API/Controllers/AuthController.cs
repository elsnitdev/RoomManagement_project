using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using RoomManagement.API.Authentication;
using RoomManagement.API.Contracts.Auth;
using RoomManagement.API.Contracts.Users;
using RoomManagement.Business.DTOs.Users;
using RoomManagement.Business.Exceptions;
using RoomManagement.Business.Interfaces;

namespace RoomManagement.API.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IUserService userService, ILogger<AuthController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        [FromServices] IAuthenticationService authenticationService,
        [FromServices] JwtTokenService tokenService,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await authenticationService.AuthenticateAsync(request.UserNameOrEmail, request.Password, cancellationToken);
            if (user is null)
                return Unauthorized(new { message = "Thông tin đăng nhập không hợp lệ hoặc tài khoản chưa được phép truy cập." });

            return Ok(tokenService.Create(user));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Không thể xử lý yêu cầu đăng nhập.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Hệ thống gặp sự cố. Vui lòng thử lại sau." });
        }
    }

    /// <summary>
    /// API cho phép người dùng tự đăng ký trở thành chủ trọ (Landlord).
    /// Bắt buộc điền đầy đủ cả thông tin tài khoản và thông tin cá nhân.
    /// Sau khi đăng ký thành công, tài khoản ở trạng thái Chờ duyệt (Pending)
    /// và chưa có quyền quản lý khu trọ cho tới khi Admin phê duyệt.
    /// </summary>
    /// <param name="request">Dữ liệu đăng ký gồm tài khoản và hồ sơ cá nhân.</param>
    /// <param name="cancellationToken">Token hỗ trợ hủy tác vụ bất đồng bộ.</param>
    /// <returns>
    /// - 201 Created: Đăng ký thành công, tài khoản đang chờ duyệt.
    /// - 400 Bad Request: Dữ liệu không hợp lệ hoặc thiếu thông tin bắt buộc.
    /// - 409 Conflict: Trùng tên đăng nhập, email hoặc số CCCD.
    /// </returns>
    [HttpPost("register-landlord")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegisterLandlord(
        [FromBody] RegisterLandlordRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Nhận yêu cầu tự đăng ký chủ trọ từ UserName: '{UserName}', Email: '{Email}'",
            request.UserName, request.Email);

        try
        {
            // 1. Chuyển đổi dữ liệu từ Request Contract sang Business DTO
            var registerDto = new RegisterLandlordDto
            {
                UserName = request.UserName,
                Email = request.Email,
                Password = request.Password,
                FullName = request.FullName,
                PhoneNumber = request.PhoneNumber,
                CitizenId = request.CitizenId,
                DateOfBirth = request.DateOfBirth,
                Address = request.Address
            };

            // 2. Gọi tầng Business để thực hiện nghiệp vụ đăng ký và lưu trữ
            var createdUserDto = await _userService.RegisterLandlordAsync(registerDto, cancellationToken);

            // 3. Chuyển đổi kết quả sang UserResponse để trả về cho Client
            var response = UserResponse.FromDto(createdUserDto);

            // 4. Trả về mã 201 Created thông báo đã nhận yêu cầu và đang chờ duyệt
            return StatusCode(StatusCodes.Status201Created, new
            {
                message = "Đăng ký tài khoản chủ trọ thành công. Tài khoản của bạn đang ở trạng thái chờ Quản trị viên (Admin) xét duyệt.",
                data = response
            });
        }
        catch (AppValidationException ex)
        {
            // Bắt lỗi vi phạm quy tắc nghiệp vụ (thiếu trường, sai định dạng CCCD/SĐT, chưa đủ tuổi...)
            _logger.LogWarning(ex, "Lỗi kiểm tra tính hợp lệ khi đăng ký chủ trọ: {Message}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (AppConflictException ex)
        {
            // Bắt lỗi trùng lặp (UserName, Email hoặc CCCD đã tồn tại)
            _logger.LogWarning(ex, "Lỗi xung đột dữ liệu khi đăng ký chủ trọ: {Message}", ex.Message);
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            // Ghi log lỗi hệ thống không lường trước
            _logger.LogError(ex, "Đã xảy ra lỗi không xác định khi đăng ký tài khoản chủ trọ.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Hệ thống gặp sự cố trong quá trình xử lý đăng ký. Vui lòng thử lại sau."
            });
        }
    }

    [HttpPost("register")]
    public IActionResult Register([FromBody] RegisterRequest request)
    {
        // TODO: kiểm tra trùng tài khoản, băm mật khẩu và tạo người dùng với quyền phù hợp.
        return StatusCode(StatusCodes.Status501NotImplemented, new
        {
            message = "API đăng ký chung chưa được triển khai. Vui lòng sử dụng /api/auth/register-landlord để đăng ký chủ trọ."
        });
    }
}
