using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomManagement.API.Contracts.Common;
using RoomManagement.API.Contracts.Rooms;
using RoomManagement.Business.DTOs;
using RoomManagement.Business.Services;
using RoomManagement.Domain;
using RoomManagement.Domain.Enums;

namespace RoomManagement.API.Controllers;

[ApiController]
[Authorize(Roles = UserRoles.Landlord)]
[Produces("application/json")]
[Route("api/properties/{propertyId:guid}/rooms")]
public sealed class RoomsController : ControllerBase
{
    private readonly RoomService _service;
    private readonly RoomBatchService _batchService;
    public RoomsController(RoomService service, RoomBatchService batchService)
    {
        _service = service;
        _batchService = batchService;
    }

    [HttpPost("batch/preview")]
    public async Task<IActionResult> PreviewBatch(Guid propertyId, [FromBody] RoomBatchPreviewRequest request, CancellationToken ct) =>
        Ok(new ApiResponse<RoomBatchPreviewResult>(await _batchService.PreviewAsync(propertyId, request.ToInput(), ct)));

    [HttpPost("batch")]
    public async Task<IActionResult> CreateBatch(Guid propertyId, [FromBody] RoomBatchCreateRequest request, CancellationToken ct)
    {
        var result = await _batchService.CreateAsync(propertyId, request.ToInput(), request.PreviewFingerprint, request.IdempotencyKey, ct);
        return StatusCode(StatusCodes.Status201Created,
            new ApiResponse<RoomBatchCreateResponse>(new(result.Rooms.Select(RoomResponse.From).ToList())));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<RoomResponse>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(Guid propertyId, [FromBody] CreateRoomRequest request, CancellationToken ct)
    {
        var result = RoomResponse.From(await _service.CreateAsync(propertyId, request.ToInput(), ct));
        return CreatedAtAction(nameof(Get), new { propertyId, roomId = result.Id }, new ApiResponse<RoomResponse>(result));
    }

    [HttpGet]
    [ProducesResponseType(typeof(PageResponse<RoomResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PageResponse<RoomResponse>>> List(
        Guid propertyId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, [FromQuery] RoomStatus? status = null,
        [FromQuery] Guid? rowId = null, [FromQuery] int? floor = null,
        CancellationToken ct = default)
    {
        var result = await _service.ListAsync(propertyId, new RoomFilter(page, pageSize, search, status, rowId, floor), ct);
        return Ok(new PageResponse<RoomResponse>(result.Items.Select(RoomResponse.From).ToList(),
            new PageMeta(page, pageSize, result.TotalItems, (int)Math.Ceiling((double)result.TotalItems / pageSize))));
    }

    [HttpGet("{roomId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<RoomResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<RoomResponse>>> Get(Guid propertyId, Guid roomId, CancellationToken ct) =>
        Ok(new ApiResponse<RoomResponse>(RoomResponse.From(await _service.GetAsync(propertyId, roomId, ct))));

    [HttpPut("{roomId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<RoomResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<RoomResponse>>> Update(Guid propertyId, Guid roomId, [FromBody] UpdateRoomRequest request, CancellationToken ct) =>
        Ok(new ApiResponse<RoomResponse>(RoomResponse.From(await _service.UpdateAsync(propertyId, roomId, request.ToInput(), ct))));

    [HttpDelete("{roomId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid propertyId, Guid roomId, CancellationToken ct)
    {
        await _service.DeleteAsync(propertyId, roomId, ct);
        return NoContent();
    }
}
