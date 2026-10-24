using Microsoft.AspNetCore.Mvc;
using RoomManagement.API.Contracts.Users;
using RoomManagement.Business.DTOs.Users;
using RoomManagement.Business.Exceptions;
using RoomManagement.Business.Interfaces;

namespace RoomManagement.API.Controllers;

/// <summary>
/// Controller xử lý các tác vụ quản trị tài khoản người dùng dành riêng cho Admin.
/// </summary>
[ApiController]
[Route("api/admin/users")]
// Khi hệ thống hoàn thiện việc cấp phát JWT Token, chỉ cần bật [Authorize(Roles = UserRoles.Admin)]
// để bảo vệ API này, đảm bảo chỉ có Quản trị viên mới được phép gọi.
// [Authorize(Roles = UserRoles.Admin)]
public sealed class AdminUsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<AdminUsersController> _logger;

    public AdminUsersController(
        IUserService userService,
        ILogger<AdminUsersController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    /// <summary>
    /// API tạo tài khoản mới và chỉ định vai trò (Landlord hoặc Tenant) cho người dùng.
    /// </summary>
    /// <param name="request">Thông tin tài khoản cần tạo được gửi từ Admin qua HTTP Request Body.</param>
    /// <param name="cancellationToken">Token hỗ trợ hủy tác vụ bất đồng bộ.</param>
    /// <returns>
    /// - 201 Created: Tạo tài khoản thành công kèm thông tin chi tiết.
    /// - 400 Bad Request: Dữ liệu không hợp lệ hoặc vai trò chỉ định không được phép.
    /// - 409 Conflict: Tên đăng nhập hoặc Email đã tồn tại trong hệ thống.
    /// </returns>
    [HttpPost]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateUser(
        [FromBody] AdminCreateUserRequest request,
        CancellationToken cancellationToken)
    {
        // Ghi log thông tin yêu cầu tạo tài khoản mới từ Admin
        _logger.LogInformation("Nhận yêu cầu tạo người dùng mới: UserName = '{UserName}', Email = '{Email}', Role = '{Role}'",
            request.UserName, request.Email, request.Role);

        try
        {
            // 1. Chuyển đổi dữ liệu từ Request Contract sang Business DTO
            var createUserDto = new CreateUserDto
            {
                UserName = request.UserName,
                Email = request.Email,
                Password = request.Password,
                Role = request.Role
            };

            // 2. Gọi tầng Business để thực thi logic nghiệp vụ (kiểm tra, băm mật khẩu, lưu DB)
            var createdUserDto = await _userService.CreateUserByAdminAsync(createUserDto, cancellationToken);

            // 3. Chuyển đổi kết quả từ DTO sang UserResponse để trả về cho Client
            var response = new UserResponse
            {
                Id = createdUserDto.Id,
                UserName = createdUserDto.UserName,
                Email = createdUserDto.Email,
                Role = createdUserDto.Role,
                IsActive = createdUserDto.IsActive,
                CreatedAt = createdUserDto.CreatedAt
            };

            // 4. Trả về mã 201 Created cùng dữ liệu phản hồi
            return StatusCode(StatusCodes.Status201Created, new
            {
                message = $"Tạo tài khoản '{response.UserName}' với vai trò '{response.Role}' thành công.",
                data = response
            });
        }
        catch (AppValidationException ex)
        {
            // Bắt lỗi vi phạm quy tắc nghiệp vụ (ví dụ: cố tình cấp quyền Admin, mật khẩu quá ngắn...)
            _logger.LogWarning(ex, "Lỗi kiểm tra tính hợp lệ nghiệp vụ: {Message}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (AppConflictException ex)
        {
            // Bắt lỗi trùng lặp dữ liệu (trùng tên đăng nhập hoặc email)
            _logger.LogWarning(ex, "Lỗi xung đột dữ liệu: {Message}", ex.Message);
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            // Bắt các lỗi phát sinh không mong muốn và ghi log lỗi hệ thống
            _logger.LogError(ex, "Đã xảy ra lỗi không xác định khi tạo người dùng.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Hệ thống gặp sự cố trong quá trình xử lý. Vui lòng thử lại sau."
            });
        }
    }
}
