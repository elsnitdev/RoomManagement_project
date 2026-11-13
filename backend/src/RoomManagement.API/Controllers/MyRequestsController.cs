using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomManagement.API.Contracts.Common;
using RoomManagement.API.Contracts.Requests;
using RoomManagement.Business.DTOs;
using RoomManagement.Business.Interfaces;
using RoomManagement.Domain;
using RoomManagement.Domain.Enums;

namespace RoomManagement.API.Controllers;

[ApiController]
[Authorize(Roles = UserRoles.Tenant)]
[Route("api/me")]
public sealed class MyRequestsController : ControllerBase
{
    private readonly IRequestService _service;

    public MyRequestsController(IRequestService service)
    {
        _service = service;
    }

    private string CurrentUserId => User.FindFirst("sub")?.Value ?? string.Empty;

    [HttpGet("requests/properties")]
    public async Task<IActionResult> ListRequestProperties(CancellationToken ct = default)
    {
        var properties = await _service.GetTenantActivePropertiesAsync(CurrentUserId, ct);
        return Ok(new ApiResponse<IReadOnlyList<TenantRequestPropertyOptionDto>>(properties));
    }

    [HttpPost("requests")]
    public async Task<IActionResult> CreateRequest(
        [FromBody] CreateTenantRequestRequest request,
        CancellationToken ct = default)
    {
        var created = await _service.CreateTenantRequestAsync(CurrentUserId, request.ToInput(), ct);
        return CreatedAtAction(
            nameof(GetRequestById),
            new { requestId = created.Id },
            new ApiResponse<TenantRequestItemDto>(created));
    }

    [HttpGet("requests")]
    public async Task<IActionResult> ListRequests(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] RequestRecipientStatus? status = null,
        CancellationToken ct = default)
    {
        var result = await _service.ListTenantRequestsAsync(
            CurrentUserId,
            new TenantRequestFilter(page, pageSize, status),
            ct);

        return Ok(new PageResponse<TenantRequestItemDto>(
            result.Items,
            new PageMeta(
                page,
                pageSize,
                result.TotalItems,
                (int)Math.Ceiling((double)result.TotalItems / pageSize))));
    }

    [HttpGet("requests/{requestId:guid}")]
    public async Task<IActionResult> GetRequestById(
        Guid requestId,
        CancellationToken ct = default)
    {
        var item = await _service.GetTenantRequestByIdAsync(CurrentUserId, requestId, ct);
        return Ok(new ApiResponse<TenantRequestItemDto>(item));
    }

    [HttpGet("announcements")]
    public async Task<IActionResult> ListAnnouncements(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool? isRead = null,
        CancellationToken ct = default)
    {
        var result = await _service.ListTenantAnnouncementsAsync(
            CurrentUserId,
            new TenantAnnouncementFilter(page, pageSize, isRead),
            ct);

        return Ok(new PageResponse<TenantAnnouncementItemDto>(
            result.Items,
            new PageMeta(
                page,
                pageSize,
                result.TotalItems,
                (int)Math.Ceiling((double)result.TotalItems / pageSize))));
    }

    [HttpPost("announcements/{requestId:guid}/read")]
    public async Task<IActionResult> MarkAnnouncementAsRead(
        Guid requestId,
        CancellationToken ct = default)
    {
        var item = await _service.MarkAnnouncementAsReadAsync(CurrentUserId, requestId, ct);
        return Ok(new ApiResponse<TenantAnnouncementItemDto>(item));
    }
}

