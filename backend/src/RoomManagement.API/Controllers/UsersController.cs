using Microsoft.AspNetCore.Mvc;
using RoomManagement.API.Contracts.Users;
using RoomManagement.Business.DTOs.Users;
using RoomManagement.Business.Exceptions;
using RoomManagement.Business.Interfaces;

namespace RoomManagement.API.Controllers;

/// <summary>
/// Controller xử lý các nghiệp vụ hồ sơ cá nhân của người dùng:
///  - Kiểm tra xem hồ sơ đã đủ các trường bắt buộc chưa (dùng khi đăng nhập lần đầu).
///  - Hoàn thiện / cập nhật hồ sơ cá nhân.
/// </summary>
[ApiController]
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
    /// - 200 OK: Trả về trạng thái hồ sơ (isProfileCompleted) và danh sách trường thiếu.
    /// - 404 Not Found: Người dùng không tồn tại.
    /// </returns>
    [HttpGet("{id}/profile-status")]
    [ProducesResponseType(typeof(ProfileStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfileStatus(
        [FromRoute] string id,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Kiểm tra tình trạng hồ sơ cho tài khoản ID: '{UserId}'", id);

        try
        {
            var statusDto = await _userService.GetProfileStatusAsync(id, cancellationToken);
            var response = ProfileStatusResponse.FromDto(statusDto);

            return Ok(response);
        }
        catch (AppNotFoundException ex)
        {
            _logger.LogWarning(ex, "Không tìm thấy người dùng: {Message}", ex.Message);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Đã xảy ra lỗi khi kiểm tra tình trạng hồ sơ.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Hệ thống gặp sự cố trong quá trình kiểm tra hồ sơ. Vui lòng thử lại sau."
            });
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
    /// - 200 OK: Cập nhật thành công và hồ sơ đã hoàn thiện.
    /// - 400 Bad Request: Dữ liệu không hợp lệ hoặc vẫn còn thiếu trường bắt buộc.
    /// - 404 Not Found: Người dùng không tồn tại.
    /// - 409 Conflict: Số CCCD cập nhật bị trùng với tài khoản khác.
    /// </returns>
    [HttpPut("{id}/profile")]
    [ProducesResponseType(typeof(ProfileStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CompleteProfile(
        [FromRoute] string id,
        [FromBody] CompleteProfileRequest request,
        CancellationToken cancellationToken)
    {
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

            // 3. Trả về kết quả thành công kèm tình trạng hồ sơ mới nhất
            return Ok(new
            {
                message = "Cập nhật thông tin hồ sơ cá nhân thành công.",
                data = response
            });
        }
        catch (AppValidationException ex)
        {
            _logger.LogWarning(ex, "Lỗi hợp lệ hồ sơ: {Message}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (AppNotFoundException ex)
        {
            _logger.LogWarning(ex, "Không tìm thấy người dùng: {Message}", ex.Message);
            return NotFound(new { message = ex.Message });
        }
        catch (AppConflictException ex)
        {
            _logger.LogWarning(ex, "Xung đột CCCD: {Message}", ex.Message);
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Đã xảy ra lỗi khi hoàn thiện hồ sơ.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Hệ thống gặp sự cố trong quá trình cập nhật hồ sơ. Vui lòng thử lại sau."
            });
        }
    }
}
