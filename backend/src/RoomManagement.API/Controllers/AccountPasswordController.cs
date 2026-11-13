using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomManagement.API.Contracts.Auth;
using RoomManagement.API.Contracts.Common;
using RoomManagement.API.Contracts.Users;
using RoomManagement.API.Filters;
using RoomManagement.Business.Interfaces;

namespace RoomManagement.API.Controllers;

[ApiController]
[Authorize]
[AllowMustChangePassword]
[Route("api/auth")]
public sealed class AccountPasswordController : ControllerBase
{
    [HttpPost("change-password")]
    [ProducesResponseType(typeof(ApiResponse<UserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        [FromServices] IAuthenticationService authenticationService,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var updated = await authenticationService.ChangePasswordAsync(
            userId,
            request.CurrentPassword,
            request.NewPassword,
            request.ConfirmPassword,
            cancellationToken);

        return Ok(new ApiResponse<UserResponse>(UserResponse.FromDto(updated)));
    }
}
