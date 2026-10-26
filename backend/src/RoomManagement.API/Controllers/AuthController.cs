using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using RoomManagement.API.Authentication;
using RoomManagement.API.Common;
using RoomManagement.API.Contracts.Auth;
using RoomManagement.API.Contracts.Common;
using RoomManagement.API.Contracts.Users;
using RoomManagement.Business.DTOs.Users;
using RoomManagement.Business.Exceptions;
using RoomManagement.Business.Interfaces;

namespace RoomManagement.API.Controllers;

/// <summary>
/// Controller xử lý các tác vụ xác thực danh tính và đăng ký tài khoản.
/// Tuân thủ quy chuẩn Clean Architecture và tài liệu ai-feature-workflow-guide.md:
/// - Xử lý đăng nhập, cấp phát JWT Token.
/// - Xử lý đăng ký chủ trọ chờ duyệt.
/// - Ánh xạ các kết quả thành công sang envelope chuẩn { data } và lỗi sang ProblemDetails.
/// </summary>
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

    /// <summary>
    /// API đăng nhập hệ thống cho mọi vai trò (Admin, Landlord, Tenant).
    /// </summary>
    /// <param name="request">Chứa thông tin đăng nhập (UserName/Email và mật khẩu).</param>
    /// <param name="authenticationService">Dịch vụ xác thực tài khoản và kiểm tra trạng thái hoạt động.</param>
    /// <param name="tokenService">Dịch vụ tạo JWT Access Token.</param>
    /// <param name="cancellationToken">Token hỗ trợ hủy tác vụ.</param>
    /// <returns>
    /// - 200 OK: Trả về LoginResponse chứa AccessToken và User info (hỗ trợ cả envelope data).
    /// - 401 Unauthorized: Thông tin đăng nhập sai hoặc tài khoản chưa được duyệt/kích hoạt.
    /// </returns>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
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
            {
                // Thông báo lỗi chung thống nhất để chống rò rỉ sự tồn tại của tài khoản
                return Unauthorized(ProblemDetailsHelper.Create(
                    StatusCodes.Status401Unauthorized,
                    "Đăng nhập không thành công",
                    "Thông tin đăng nhập không hợp lệ hoặc tài khoản chưa được phép truy cập.",
                    code: "INVALID_CREDENTIALS"));
            }

            var loginResponse = tokenService.Create(user);
            return Ok(loginResponse);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Không thể xử lý yêu cầu đăng nhập.");
            return StatusCode(StatusCodes.Status500InternalServerError, ProblemDetailsHelper.Create(
                StatusCodes.Status500InternalServerError,
                "Lỗi máy chủ nội bộ",
                "Hệ thống gặp sự cố. Vui lòng thử lại sau.",
                code: "INTERNAL_SERVER_ERROR"));
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
    /// - 201 Created: Đăng ký thành công, tài khoản đang chờ duyệt (bọc trong { data }).
    /// - 400 Bad Request: Dữ liệu không hợp lệ hoặc thiếu thông tin bắt buộc.
    /// - 409 Conflict: Trùng tên đăng nhập, email hoặc số CCCD.
    /// - 422 Unprocessable Entity: Vi phạm quy tắc nghiệp vụ đăng ký.
    /// </returns>
    [HttpPost("register-landlord")]
    [ProducesResponseType(typeof(ApiResponse<UserResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
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

            // 4. Trả về mã 201 Created bọc trong envelope { data } chuẩn kiến trúc
            return StatusCode(StatusCodes.Status201Created, new
            {
                data = response,
                message = "Đăng ký tài khoản chủ trọ thành công. Tài khoản của bạn đang ở trạng thái chờ Quản trị viên (Admin) xét duyệt."
            });
        }
        catch (BusinessRuleException ex)
        {
            _logger.LogWarning(ex, "Vi phạm quy tắc nghiệp vụ khi đăng ký chủ trọ: {Message}", ex.Message);
            return UnprocessableEntity(ProblemDetailsHelper.Create(
                StatusCodes.Status422UnprocessableEntity,
                "Vi phạm quy tắc nghiệp vụ",
                ex.Message,
                code: ex.Code ?? "BUSINESS_RULE_VIOLATION"));
        }
        catch (AppValidationException ex)
        {
            _logger.LogWarning(ex, "Lỗi kiểm tra tính hợp lệ khi đăng ký chủ trọ: {Message}", ex.Message);
            return BadRequest(ProblemDetailsHelper.Create(
                StatusCodes.Status400BadRequest,
                "Dữ liệu không hợp lệ",
                ex.Message,
                code: ex.Code ?? "VALIDATION_FAILED"));
        }
        catch (AppConflictException ex)
        {
            _logger.LogWarning(ex, "Lỗi xung đột dữ liệu khi đăng ký chủ trọ: {Message}", ex.Message);
            return Conflict(ProblemDetailsHelper.Create(
                StatusCodes.Status409Conflict,
                "Xung đột dữ liệu",
                ex.Message,
                code: ex.Code ?? "DUPLICATE_DATA"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Đã xảy ra lỗi không xác định khi đăng ký tài khoản chủ trọ.");
            return StatusCode(StatusCodes.Status500InternalServerError, ProblemDetailsHelper.Create(
                StatusCodes.Status500InternalServerError,
                "Lỗi máy chủ nội bộ",
                "Hệ thống gặp sự cố trong quá trình xử lý đăng ký. Vui lòng thử lại sau.",
                code: "INTERNAL_SERVER_ERROR"));
        }
    }

    /// <summary>
    /// API đăng ký tài khoản chung (chưa triển khai).
    /// </summary>
    [HttpPost("register")]
    public IActionResult Register([FromBody] RegisterRequest request)
    {
        return StatusCode(StatusCodes.Status501NotImplemented, ProblemDetailsHelper.Create(
            StatusCodes.Status501NotImplemented,
            "Chưa triển khai",
            "API đăng ký chung chưa được triển khai. Vui lòng sử dụng /api/auth/register-landlord để đăng ký chủ trọ.",
            code: "NOT_IMPLEMENTED"));
    }
}
