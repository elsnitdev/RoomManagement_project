using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using RoomManagement.API.Common;
using RoomManagement.API.Contracts.Common;
using RoomManagement.API.Contracts.Users;
using RoomManagement.Business.DTOs.Users;
using RoomManagement.Business.Exceptions;
using RoomManagement.Business.Interfaces;

namespace RoomManagement.API.Controllers;

/// <summary>
/// Controller xử lý các nghiệp vụ hồ sơ cá nhân của người dùng:
///  - Kiểm tra xem hồ sơ đã đủ các trường bắt buộc chưa (dùng khi đăng nhập lần đầu).
///  - Hoàn thiện / cập nhật hồ sơ cá nhân.
/// Tuân thủ quy chuẩn Clean Architecture và tài liệu ai-feature-workflow-guide.md:
///  - Phản hồi dữ liệu bọc trong envelope chuẩn { data }.
///  - Phản hồi lỗi chuẩn hóa theo ProblemDetails (400, 404, 409, 422).
/// </summary>
[ApiController]
[Authorize]
[Route("api/users")]
public sealed class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        IUserService userService,
        ILogger<UsersController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    /// <summary>
    /// API kiểm tra tình trạng hoàn thiện hồ sơ của người dùng.
    /// Dùng cho luồng đăng nhập lần đầu (đặc biệt là tài khoản do Admin cấp):
    /// Hệ thống kiểm tra xem người dùng đã điền đủ các trường bắt buộc hay chưa,
    /// nếu chưa sẽ trả về danh sách các trường còn thiếu (missingFields).
    /// </summary>
    /// <param name="id">ID của người dùng cần kiểm tra.</param>
    /// <param name="cancellationToken">Token hỗ trợ hủy tác vụ.</param>
    /// <returns>
    /// - 200 OK: Trả về trạng thái hồ sơ (isProfileCompleted) bọc trong envelope { data }.
    /// - 404 Not Found: Người dùng không tồn tại.
    /// </returns>
    [HttpGet("{id}/profile-status")]
    [ProducesResponseType(typeof(ApiResponse<ProfileStatusResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfileStatus(
        [FromRoute] string id,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(User.FindFirst("sub")?.Value, id, StringComparison.Ordinal))
            return Forbid();

        _logger.LogInformation("Kiểm tra tình trạng hồ sơ cho tài khoản ID: '{UserId}'", id);

        try
        {
            var statusDto = await _userService.GetProfileStatusAsync(id, cancellationToken);
            var response = ProfileStatusResponse.FromDto(statusDto);

            // Trả về envelope { data } chuẩn kiến trúc và giữ các trường trực tiếp tương thích ngược
            return Ok(new
            {
                data = response,
                isProfileCompleted = response.IsProfileCompleted,
                missingFields = response.MissingFields,
                userId = response.UserId
            });
        }
        catch (AppNotFoundException ex)
        {
            _logger.LogWarning(ex, "Không tìm thấy người dùng: {Message}", ex.Message);
            return NotFound(ProblemDetailsHelper.Create(
                StatusCodes.Status404NotFound,
                "Không tìm thấy tài nguyên",
                ex.Message,
                code: ex.Code ?? "USER_NOT_FOUND"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Đã xảy ra lỗi khi kiểm tra tình trạng hồ sơ.");
            return StatusCode(StatusCodes.Status500InternalServerError, ProblemDetailsHelper.Create(
                StatusCodes.Status500InternalServerError,
                "Lỗi máy chủ nội bộ",
                "Hệ thống gặp sự cố trong quá trình kiểm tra hồ sơ. Vui lòng thử lại sau.",
                code: "INTERNAL_SERVER_ERROR"));
        }
    }

    /// <summary>
    /// API cho phép người dùng điền hoặc cập nhật thông tin cá nhân.
    /// Được gọi khi người dùng đăng nhập lần đầu và được yêu cầu điền đủ hồ sơ,
    /// hoặc khi người dùng muốn cập nhật thông tin sau này.
    /// Sau khi cập nhật, hồ sơ bắt buộc phải đầy đủ các trường theo vai trò.
    /// </summary>
    /// <param name="id">ID của người dùng cần cập nhật.</param>
    /// <param name="request">Thông tin cập nhật (chỉ gửi trường cần cập nhật, giữ nguyên các trường khác).</param>
    /// <param name="cancellationToken">Token hỗ trợ hủy tác vụ.</param>
    /// <returns>
    /// - 200 OK: Cập nhật thành công và hồ sơ đã hoàn thiện (bọc trong { data }).
    /// - 400 Bad Request: Dữ liệu không hợp lệ hoặc vẫn còn thiếu trường bắt buộc.
    /// - 404 Not Found: Người dùng không tồn tại.
    /// - 409 Conflict: Số CCCD cập nhật bị trùng với tài khoản khác.
    /// - 422 Unprocessable Entity: Vi phạm quy tắc nghiệp vụ cập nhật hồ sơ.
    /// </returns>
    [HttpPut("{id}/profile")]
    [ProducesResponseType(typeof(ApiResponse<ProfileStatusResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CompleteProfile(
        [FromRoute] string id,
        [FromBody] CompleteProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(User.FindFirst("sub")?.Value, id, StringComparison.Ordinal))
            return Forbid();

        _logger.LogInformation("Nhận yêu cầu hoàn thiện/cập nhật hồ sơ cho tài khoản ID: '{UserId}'", id);

        try
        {
            // 1. Chuyển đổi từ Request Contract sang Business DTO
            var dto = new CompleteProfileDto
            {
                FullName = request.FullName,
                PhoneNumber = request.PhoneNumber,
                CitizenId = request.CitizenId,
                DateOfBirth = request.DateOfBirth,
                Address = request.Address
            };

            // 2. Gọi tầng Business cập nhật và kiểm tra tính đầy đủ
            var statusDto = await _userService.CompleteProfileAsync(id, dto, cancellationToken);
            var response = ProfileStatusResponse.FromDto(statusDto);

            // 3. Trả về kết quả thành công bọc trong envelope { data } chuẩn kiến trúc
            return Ok(new
            {
                data = response,
                message = "Cập nhật thông tin hồ sơ cá nhân thành công."
            });
        }
        catch (BusinessRuleException ex)
        {
            _logger.LogWarning(ex, "Vi phạm quy tắc nghiệp vụ cập nhật hồ sơ: {Message}", ex.Message);
            return UnprocessableEntity(ProblemDetailsHelper.Create(
                StatusCodes.Status422UnprocessableEntity,
                "Vi phạm quy tắc nghiệp vụ",
                ex.Message,
                code: ex.Code ?? "BUSINESS_RULE_VIOLATION"));
        }
        catch (AppValidationException ex)
        {
            _logger.LogWarning(ex, "Lỗi hợp lệ hồ sơ: {Message}", ex.Message);
            return BadRequest(ProblemDetailsHelper.Create(
                StatusCodes.Status400BadRequest,
                "Dữ liệu không hợp lệ",
                ex.Message,
                code: ex.Code ?? "VALIDATION_FAILED"));
        }
        catch (AppNotFoundException ex)
        {
            _logger.LogWarning(ex, "Không tìm thấy người dùng: {Message}", ex.Message);
            return NotFound(ProblemDetailsHelper.Create(
                StatusCodes.Status404NotFound,
                "Không tìm thấy tài nguyên",
                ex.Message,
                code: ex.Code ?? "USER_NOT_FOUND"));
        }
        catch (AppConflictException ex)
        {
            _logger.LogWarning(ex, "Xung đột CCCD: {Message}", ex.Message);
            return Conflict(ProblemDetailsHelper.Create(
                StatusCodes.Status409Conflict,
                "Xung đột dữ liệu",
                ex.Message,
                code: ex.Code ?? "CITIZEN_ID_CONFLICT"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Đã xảy ra lỗi khi hoàn thiện hồ sơ.");
            return StatusCode(StatusCodes.Status500InternalServerError, ProblemDetailsHelper.Create(
                StatusCodes.Status500InternalServerError,
                "Lỗi máy chủ nội bộ",
                "Hệ thống gặp sự cố trong quá trình cập nhật hồ sơ. Vui lòng thử lại sau.",
                code: "INTERNAL_SERVER_ERROR"));
        }
    }
}
