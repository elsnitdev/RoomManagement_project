using Microsoft.AspNetCore.Mvc;
using RoomManagement.API.Contracts.Common;
using RoomManagement.API.Contracts.Rooms;
using RoomManagement.Business.DTOs;
using RoomManagement.Business.Services;
using RoomManagement.Domain.Enums;

namespace RoomManagement.API.Controllers;

[ApiController]
[Produces("application/json")]
[Route("api/properties/{propertyId:guid}/rooms")]
public sealed class RoomsController : ControllerBase
{
    private readonly RoomService _service;
    public RoomsController(RoomService service) => _service = service;

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
        CancellationToken ct = default)
    {
        var result = await _service.ListAsync(propertyId, new RoomFilter(page, pageSize, search, status), ct);
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
