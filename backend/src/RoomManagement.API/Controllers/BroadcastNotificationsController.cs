using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomManagement.API.Contracts.Common;
using RoomManagement.API.Contracts.Notifications;
using RoomManagement.Business.DTOs;
using RoomManagement.Business.Interfaces;
using RoomManagement.Domain;
using RoomManagement.Domain.Enums;

namespace RoomManagement.API.Controllers;

[ApiController]
[Authorize]
public sealed class BroadcastNotificationsController : ControllerBase
{
    private readonly INotificationService _service;

    public BroadcastNotificationsController(INotificationService service)
    {
        _service = service;
    }

    private string CurrentUserId => User.FindFirst("sub")?.Value ?? string.Empty;

    [HttpGet("api/admin/notifications/targets")]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<IActionResult> GetAdminTargets(
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var result = await _service.GetAdminTargetsAsync(CurrentUserId, search, ct);
        return Ok(new ApiResponse<AdminNotificationTargetsDto>(result));
    }

    [HttpPost("api/admin/notifications/send")]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<IActionResult> SendAdminNotification(
        [FromBody] SendAdminNotificationRequest request,
        CancellationToken ct = default)
    {
        var result = await _service.SendAdminNotificationAsync(CurrentUserId, request.ToInput(), ct);
        return Ok(new ApiResponse<SendNotificationResultDto>(result));
    }

    [HttpPost("api/admin/notifications/broadcast")]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<IActionResult> AdminBroadcast(
        [FromBody] LegacyBroadcastRequest input,
        CancellationToken ct = default)
    {
        var result = await _service.SendAdminNotificationAsync(
            CurrentUserId,
            new SendAdminNotificationInput(
                AdminNotificationTargetScope.AllSystem,
                TargetUserId: null,
                input.Title,
                input.Message,
                input.IdempotencyKey),
            ct);

        return Ok(new ApiResponse<object>(new
        {
            recipients = result.RecipientCount,
            duplicate = result.Duplicate,
            eventKey = result.EventKey,
            type = result.Type
        }));
    }

    [HttpGet("api/operator/notifications/targets")]
    [Authorize(Roles = UserRoles.LandlordOrManager)]
    public async Task<IActionResult> GetOperatorTargets(
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var result = await _service.GetOperatorTargetsAsync(search, ct);
        return Ok(new ApiResponse<OperatorNotificationTargetsDto>(result));
    }

    [HttpPost("api/operator/notifications/send")]
    [Authorize(Roles = UserRoles.LandlordOrManager)]
    public async Task<IActionResult> SendOperatorNotification(
        [FromBody] SendOperatorNotificationRequest request,
        CancellationToken ct = default)
    {
        var result = await _service.SendOperatorNotificationAsync(request.ToInput(), ct);
        return Ok(new ApiResponse<SendNotificationResultDto>(result));
    }

    [HttpPost("api/properties/{propertyId:guid}/manager-announcements")]
    [Authorize(Roles = UserRoles.LandlordOrManager)]
    public async Task<IActionResult> ManagerBroadcast(
        Guid propertyId,
        [FromBody] LegacyBroadcastRequest input,
        CancellationToken ct = default)
    {
        var result = await _service.SendOperatorNotificationAsync(
            new SendOperatorNotificationInput(
                OperatorNotificationTargetScope.SpecificProperty,
                propertyId,
                TargetUserId: null,
                input.Title,
                input.Message,
                input.IdempotencyKey),
            ct);

        return Ok(new ApiResponse<object>(new
        {
            recipients = result.RecipientCount,
            duplicate = result.Duplicate,
            eventKey = result.EventKey,
            type = result.Type
        }));
    }

    [HttpGet("api/me/notifications/sent")]
    [Authorize(Roles = $"{UserRoles.Admin},{UserRoles.LandlordOrManager}")]
    public async Task<IActionResult> ListSentNotifications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _service.ListSentNotificationsAsync(CurrentUserId, page, pageSize, ct);
        return Ok(new PageResponse<SentNotificationItemDto>(
            result.Items,
            new PageMeta(
                result.Page,
                result.PageSize,
                result.TotalItems,
                (int)Math.Ceiling((double)result.TotalItems / result.PageSize))));
    }
}
