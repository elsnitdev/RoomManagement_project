using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using RoomManagement.Domain;
using RoomManagement.API.Common;
using RoomManagement.API.Contracts.Common;
using RoomManagement.API.Contracts.Users;
using RoomManagement.Business.DTOs.Users;
using RoomManagement.Business.Exceptions;
using RoomManagement.Business.Interfaces;

namespace RoomManagement.API.Controllers;

/// <summary>
/// Controller xử lý các tác vụ quản trị tài khoản người dùng dành riêng cho Admin.
/// Tuân thủ quy chuẩn Clean Architecture và tài liệu ai-feature-workflow-guide.md:
/// - Chỉ tiếp nhận HTTP request, validate cơ bản, gọi Business Service.
/// - Không truy vấn trực tiếp DbContext hay thực thi tính toán nghiệp vụ trong Controller.
/// - Phản hồi thành công đơn lẻ dùng { data }, phản hồi danh sách dùng { data, meta }.
/// - Phản hồi lỗi chuẩn hóa theo ProblemDetails (400 cho validation, 404 cho not found, 409 cho conflict, 422 cho vi phạm nghiệp vụ).
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
    /// - 201 Created: Tạo tài khoản thành công kèm thông tin chi tiết bọc trong { data }.
    /// - 400 Bad Request: Dữ liệu nhập vào không hợp lệ (AppValidationException).
    /// - 409 Conflict: Tên đăng nhập hoặc Email đã tồn tại trong hệ thống (AppConflictException).
    /// - 422 Unprocessable Entity: Vi phạm quy tắc nghiệp vụ ví dụ chỉ định sai quyền (BusinessRuleException).
    /// </returns>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<UserResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateUser(
        [FromBody] AdminCreateUserRequest request,
        CancellationToken cancellationToken)
    {
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

            // 4. Trả về mã 201 Created bọc trong envelope { data } chuẩn kiến trúc
            return StatusCode(StatusCodes.Status201Created, new
            {
                data = response,
                message = $"Tạo tài khoản '{response.UserName}' với vai trò '{response.Role}' thành công."
            });
        }
        catch (BusinessRuleException ex)
        {
            // Vi phạm quy tắc phân quyền/nghiệp vụ -> HTTP 422 Unprocessable Entity theo ai-feature-workflow-guide.md
            _logger.LogWarning(ex, "Vi phạm quy tắc nghiệp vụ tạo tài khoản: {Message}", ex.Message);
            return UnprocessableEntity(ProblemDetailsHelper.Create(
                StatusCodes.Status422UnprocessableEntity,
                "Vi phạm quy tắc nghiệp vụ",
                ex.Message,
                code: ex.Code ?? "BUSINESS_RULE_VIOLATION"));
        }
        catch (AppValidationException ex)
        {
            // Lỗi kiểm tra tính hợp lệ dữ liệu nhập -> HTTP 400 Bad Request
            _logger.LogWarning(ex, "Lỗi kiểm tra tính hợp lệ dữ liệu: {Message}", ex.Message);
            return BadRequest(ProblemDetailsHelper.Create(
                StatusCodes.Status400BadRequest,
                "Dữ liệu không hợp lệ",
                ex.Message,
                code: ex.Code ?? "VALIDATION_FAILED"));
        }
        catch (AppConflictException ex)
        {
            // Lỗi trùng lặp dữ liệu (trùng tên đăng nhập hoặc email) -> HTTP 409 Conflict
            _logger.LogWarning(ex, "Lỗi xung đột dữ liệu: {Message}", ex.Message);
            return Conflict(ProblemDetailsHelper.Create(
                StatusCodes.Status409Conflict,
                "Xung đột dữ liệu",
                ex.Message,
                code: ex.Code ?? "DUPLICATE_DATA"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Đã xảy ra lỗi không xác định khi tạo người dùng.");
            return StatusCode(StatusCodes.Status500InternalServerError, ProblemDetailsHelper.Create(
                StatusCodes.Status500InternalServerError,
                "Lỗi máy chủ nội bộ",
                "Hệ thống gặp sự cố trong quá trình xử lý. Vui lòng thử lại sau.",
                code: "INTERNAL_SERVER_ERROR"));
        }
    }

    /// <summary>
    /// API lấy danh sách tất cả các yêu cầu đăng ký chủ trọ đang ở trạng thái Chờ duyệt (Pending).
    /// Tuân thủ quy định: "danh sách phân trang dùng { data, meta }".
    /// </summary>
    [HttpGet("pending-landlords")]
    [ProducesResponseType(typeof(ApiPagedResponse<LandlordRegistrationResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingLandlords(
        [FromServices] ILandlordApprovalService approvalService,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Admin đang tải danh sách các yêu cầu đăng ký chủ trọ chờ duyệt.");

        var pendingList = await approvalService.GetPendingRegistrationsAsync(cancellationToken);
        var response = pendingList.Select(LandlordRegistrationResponse.FromDto).ToList();

        // Trả về chuẩn envelope { data, meta } theo tài liệu kiến trúc, giữ trường total tương thích
        return Ok(new
        {
            data = response,
            meta = new
            {
                total = response.Count
            },
            total = response.Count
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
    [ProducesResponseType(typeof(ApiResponse<LandlordRegistrationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
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
                data = response,
                message = $"Đã phê duyệt thành công yêu cầu của chủ trọ '{response.UserName}'. Tài khoản đã được kích hoạt."
            });
        }
        catch (BusinessRuleException ex)
        {
            _logger.LogWarning(ex, "Vi phạm quy tắc nghiệp vụ duyệt chủ trọ: {Message}", ex.Message);
            return UnprocessableEntity(ProblemDetailsHelper.Create(
                StatusCodes.Status422UnprocessableEntity,
                "Vi phạm quy tắc nghiệp vụ",
                ex.Message,
                code: ex.Code ?? "BUSINESS_RULE_VIOLATION"));
        }
        catch (AppNotFoundException ex)
        {
            _logger.LogWarning(ex, "Không tìm thấy yêu cầu: {Message}", ex.Message);
            return NotFound(ProblemDetailsHelper.Create(
                StatusCodes.Status404NotFound,
                "Không tìm thấy tài nguyên",
                ex.Message,
                code: ex.Code ?? "NOT_FOUND"));
        }
        catch (AppConflictException ex)
        {
            _logger.LogWarning(ex, "Xung đột trạng thái duyệt: {Message}", ex.Message);
            return Conflict(ProblemDetailsHelper.Create(
                StatusCodes.Status409Conflict,
                "Xung đột dữ liệu",
                ex.Message,
                code: ex.Code ?? "APPROVAL_CONFLICT"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Đã xảy ra lỗi không xác định khi duyệt yêu cầu chủ trọ.");
            return StatusCode(StatusCodes.Status500InternalServerError, ProblemDetailsHelper.Create(
                StatusCodes.Status500InternalServerError,
                "Lỗi máy chủ nội bộ",
                "Hệ thống gặp sự cố trong quá trình duyệt yêu cầu. Vui lòng thử lại sau.",
                code: "INTERNAL_SERVER_ERROR"));
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
    [ProducesResponseType(typeof(ApiResponse<LandlordRegistrationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
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
                data = response,
                message = $"Đã từ chối yêu cầu đăng ký của '{response.UserName}'."
            });
        }
        catch (BusinessRuleException ex)
        {
            _logger.LogWarning(ex, "Vi phạm quy tắc nghiệp vụ từ chối chủ trọ: {Message}", ex.Message);
            return UnprocessableEntity(ProblemDetailsHelper.Create(
                StatusCodes.Status422UnprocessableEntity,
                "Vi phạm quy tắc nghiệp vụ",
                ex.Message,
                code: ex.Code ?? "BUSINESS_RULE_VIOLATION"));
        }
        catch (AppValidationException ex)
        {
            _logger.LogWarning(ex, "Lỗi hợp lệ lý do từ chối: {Message}", ex.Message);
            return BadRequest(ProblemDetailsHelper.Create(
                StatusCodes.Status400BadRequest,
                "Dữ liệu không hợp lệ",
                ex.Message,
                code: ex.Code ?? "VALIDATION_FAILED"));
        }
        catch (AppNotFoundException ex)
        {
            _logger.LogWarning(ex, "Không tìm thấy yêu cầu: {Message}", ex.Message);
            return NotFound(ProblemDetailsHelper.Create(
                StatusCodes.Status404NotFound,
                "Không tìm thấy tài nguyên",
                ex.Message,
                code: ex.Code ?? "NOT_FOUND"));
        }
        catch (AppConflictException ex)
        {
            _logger.LogWarning(ex, "Xung đột trạng thái duyệt: {Message}", ex.Message);
            return Conflict(ProblemDetailsHelper.Create(
                StatusCodes.Status409Conflict,
                "Xung đột dữ liệu",
                ex.Message,
                code: ex.Code ?? "APPROVAL_CONFLICT"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Đã xảy ra lỗi không xác định khi từ chối yêu cầu chủ trọ.");
            return StatusCode(StatusCodes.Status500InternalServerError, ProblemDetailsHelper.Create(
                StatusCodes.Status500InternalServerError,
                "Lỗi máy chủ nội bộ",
                "Hệ thống gặp sự cố trong quá trình từ chối yêu cầu. Vui lòng thử lại sau.",
                code: "INTERNAL_SERVER_ERROR"));
        }
    }
}
