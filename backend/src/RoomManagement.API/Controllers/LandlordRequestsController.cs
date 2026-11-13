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
[Authorize(Roles = UserRoles.Landlord)]
public sealed class LandlordRequestsController : ControllerBase
{
    private readonly IRequestService _service;

    public LandlordRequestsController(IRequestService service)
    {
        _service = service;
    }

    [HttpGet("api/landlord/requests")]
    public async Task<IActionResult> ListRequests(
        [FromQuery] Guid? propertyId = null,
        [FromQuery] RequestRecipientStatus? status = null,
        [FromQuery] bool? isRead = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _service.ListLandlordRequestsAsync(
            new LandlordRequestFilter(page, pageSize, propertyId, status, isRead, search),
            ct);

        return Ok(new PageResponse<LandlordRequestItemDto>(
            result.Items,
            new PageMeta(
                page,
                pageSize,
                result.TotalItems,
                (int)Math.Ceiling((double)result.TotalItems / pageSize))));
    }

    [HttpGet("api/landlord/requests/{requestId:guid}")]
    public async Task<IActionResult> GetRequestById(
        Guid requestId,
        CancellationToken ct = default)
    {
        var item = await _service.GetLandlordRequestByIdAsync(requestId, ct);
        return Ok(new ApiResponse<LandlordRequestItemDto>(item));
    }

    [HttpPatch("api/landlord/requests/{requestId:guid}/status")]
    [HttpPut("api/landlord/requests/{requestId:guid}/status")]
    public async Task<IActionResult> UpdateRequestStatus(
        Guid requestId,
        [FromBody] UpdateLandlordRequestStatusRequest request,
        CancellationToken ct = default)
    {
        var item = await _service.UpdateLandlordRequestStatusAsync(requestId, request.Status, ct);
        return Ok(new ApiResponse<LandlordRequestItemDto>(item));
    }

    [HttpGet("api/properties/{propertyId:guid}/announcement-recipients")]
    public async Task<IActionResult> GetAnnouncementRecipients(
        Guid propertyId,
        CancellationToken ct = default)
    {
        var recipients = await _service.GetAnnouncementRecipientsAsync(propertyId, ct);
        return Ok(new ApiResponse<IReadOnlyList<AnnouncementRecipientOptionDto>>(recipients));
    }

    [HttpPost("api/properties/{propertyId:guid}/announcements")]
    public async Task<IActionResult> CreateAnnouncement(
        Guid propertyId,
        [FromBody] CreateLandlordAnnouncementRequest request,
        CancellationToken ct = default)
    {
        var created = await _service.CreateLandlordAnnouncementAsync(propertyId, request.ToInput(), ct);
        return StatusCode(StatusCodes.Status201Created, new ApiResponse<LandlordAnnouncementItemDto>(created));
    }

    [HttpGet("api/properties/{propertyId:guid}/announcements")]
    public async Task<IActionResult> ListAnnouncements(
        Guid propertyId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _service.ListLandlordAnnouncementsAsync(propertyId, page, pageSize, ct);
        return Ok(new PageResponse<LandlordAnnouncementItemDto>(
            result.Items,
            new PageMeta(
                page,
                pageSize,
                result.TotalItems,
                (int)Math.Ceiling((double)result.TotalItems / pageSize))));
    }
}

