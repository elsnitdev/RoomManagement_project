using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using RoomManagement.Domain;
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
[Authorize(Roles = UserRoles.Admin)]
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
            var response = UserResponse.FromDto(createdUserDto);

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

    /// <summary>
    /// API lấy danh sách tất cả các yêu cầu đăng ký chủ trọ đang ở trạng thái Chờ duyệt (Pending).
    /// Giúp Quản trị viên (Admin) xem xét và kiểm tra thông tin cá nhân của người đăng ký.
    /// </summary>
    [HttpGet("pending-landlords")]
    [ProducesResponseType(typeof(IEnumerable<LandlordRegistrationResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingLandlords(
        [FromServices] ILandlordApprovalService approvalService,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Admin đang tải danh sách các yêu cầu đăng ký chủ trọ chờ duyệt.");

        var pendingList = await approvalService.GetPendingRegistrationsAsync(cancellationToken);
        var response = pendingList.Select(LandlordRegistrationResponse.FromDto);

        return Ok(new
        {
            total = pendingList.Count,
            data = response
        });
    }

    /// <summary>
    /// API Quản trị viên (Admin) phê duyệt yêu cầu đăng ký chủ trọ.
    /// Sau khi duyệt: tài khoản được kích hoạt và chính thức có quyền Landlord để quản lý khu trọ.
    /// </summary>
    /// <param name="id">ID tài khoản chủ trọ cần duyệt.</param>
    /// <param name="approvalService">Service xử lý nghiệp vụ duyệt.</param>
    /// <param name="cancellationToken">Token hỗ trợ hủy tác vụ.</param>
    [HttpPost("pending-landlords/{id}/approve")]
    [ProducesResponseType(typeof(LandlordRegistrationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ApproveLandlord(
        [FromRoute] string id,
        [FromServices] ILandlordApprovalService approvalService,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Admin thực hiện duyệt yêu cầu đăng ký cho tài khoản ID: '{UserId}'", id);

        try
        {
            var result = await approvalService.ApproveAsync(id, cancellationToken);
            var response = LandlordRegistrationResponse.FromDto(result);

            return Ok(new
            {
                message = $"Đã phê duyệt thành công yêu cầu của chủ trọ '{response.UserName}'. Tài khoản đã được kích hoạt.",
                data = response
            });
        }
        catch (AppNotFoundException ex)
        {
            _logger.LogWarning(ex, "Không tìm thấy yêu cầu: {Message}", ex.Message);
            return NotFound(new { message = ex.Message });
        }
        catch (AppConflictException ex)
        {
            _logger.LogWarning(ex, "Xung đột trạng thái duyệt: {Message}", ex.Message);
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Đã xảy ra lỗi không xác định khi duyệt yêu cầu chủ trọ.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Hệ thống gặp sự cố trong quá trình duyệt yêu cầu. Vui lòng thử lại sau."
            });
        }
    }

    /// <summary>
    /// API Quản trị viên (Admin) từ chối yêu cầu đăng ký chủ trọ kèm lý do.
    /// Sau khi từ chối: tài khoản chuyển sang Rejected và vẫn bị khóa, không thể quản lý khu trọ.
    /// </summary>
    /// <param name="id">ID tài khoản chủ trọ cần từ chối.</param>
    /// <param name="request">Chứa lý do từ chối (bắt buộc).</param>
    /// <param name="approvalService">Service xử lý nghiệp vụ từ chối.</param>
    /// <param name="cancellationToken">Token hỗ trợ hủy tác vụ.</param>
    [HttpPost("pending-landlords/{id}/reject")]
    [ProducesResponseType(typeof(LandlordRegistrationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RejectLandlord(
        [FromRoute] string id,
        [FromBody] RejectLandlordRequest request,
        [FromServices] ILandlordApprovalService approvalService,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Admin thực hiện từ chối yêu cầu đăng ký cho tài khoản ID: '{UserId}' với lý do: {Reason}",
            id, request.Reason);

        try
        {
            var result = await approvalService.RejectAsync(id, request.Reason, cancellationToken);
            var response = LandlordRegistrationResponse.FromDto(result);

            return Ok(new
            {
                message = $"Đã từ chối yêu cầu đăng ký của '{response.UserName}'.",
                data = response
            });
        }
        catch (AppValidationException ex)
        {
            _logger.LogWarning(ex, "Lỗi hợp lệ lý do từ chối: {Message}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (AppNotFoundException ex)
        {
            _logger.LogWarning(ex, "Không tìm thấy yêu cầu: {Message}", ex.Message);
            return NotFound(new { message = ex.Message });
        }
        catch (AppConflictException ex)
        {
            _logger.LogWarning(ex, "Xung đột trạng thái duyệt: {Message}", ex.Message);
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Đã xảy ra lỗi không xác định khi từ chối yêu cầu chủ trọ.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Hệ thống gặp sự cố trong quá trình từ chối yêu cầu. Vui lòng thử lại sau."
            });
        }
    }
}
