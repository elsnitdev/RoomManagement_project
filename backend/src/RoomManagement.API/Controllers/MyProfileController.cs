using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomManagement.API.Common;
using RoomManagement.API.Contracts.Common;
using RoomManagement.API.Contracts.Users;
using RoomManagement.Business.DTOs.Users;
using RoomManagement.Business.Exceptions;
using RoomManagement.Business.Interfaces;

namespace RoomManagement.API.Controllers;

[ApiController]
[Authorize]
[Route("api/me/profile")]
public sealed class MyProfileController : ControllerBase
{
    private readonly IUserService _userService;

    public MyProfileController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Forbid();

        try
        {
            var dto = await _userService.GetMyProfileAsync(userId, cancellationToken);
            return Ok(new ApiResponse<UserResponse>(UserResponse.FromDto(dto)));
        }
        catch (AppNotFoundException ex)
        {
            return NotFound(ProblemDetailsHelper.Create(404, "Không tìm thấy tài nguyên", ex.Message, code: ex.Code ?? "USER_NOT_FOUND"));
        }
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] CompleteProfileRequest request, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Forbid();

        try
        {
            await _userService.CompleteProfileAsync(userId, new CompleteProfileDto
            {
                FullName = request.FullName,
                PhoneNumber = request.PhoneNumber,
                CitizenId = request.CitizenId,
                DateOfBirth = request.DateOfBirth,
                Address = request.Address
            }, cancellationToken);

            var updated = await _userService.GetMyProfileAsync(userId, cancellationToken);
            return Ok(new { data = UserResponse.FromDto(updated), message = "Cập nhật thông tin tài khoản thành công." });
        }
        catch (AppValidationException ex)
        {
            return BadRequest(ProblemDetailsHelper.Create(400, "Dữ liệu không hợp lệ", ex.Message, code: ex.Code ?? "VALIDATION_FAILED"));
        }
        catch (AppConflictException ex)
        {
            return Conflict(ProblemDetailsHelper.Create(409, "Xung đột dữ liệu", ex.Message, code: ex.Code ?? "CITIZEN_ID_CONFLICT"));
        }
        catch (BusinessRuleException ex)
        {
            return UnprocessableEntity(ProblemDetailsHelper.Create(422, "Vi phạm quy tắc nghiệp vụ", ex.Message, code: ex.Code ?? "BUSINESS_RULE_VIOLATION"));
        }
        catch (AppNotFoundException ex)
        {
            return NotFound(ProblemDetailsHelper.Create(404, "Không tìm thấy tài nguyên", ex.Message, code: ex.Code ?? "USER_NOT_FOUND"));
        }
    }
}
