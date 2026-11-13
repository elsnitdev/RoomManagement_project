using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomManagement.API.Contracts.Common;
using RoomManagement.API.Contracts.Rooms;
using RoomManagement.Business.Services;
using RoomManagement.Domain;

namespace RoomManagement.API.Controllers;

[ApiController]
[Authorize(Roles = UserRoles.LandlordOrManager)]
[Route("api/properties/{propertyId:guid}/rows")]
public sealed class RoomRowsController : ControllerBase
{
    private readonly RoomBatchService _service;
    public RoomRowsController(RoomBatchService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List(Guid propertyId, CancellationToken ct) =>
        Ok(new ApiResponse<IReadOnlyList<RoomRowResponse>>(
            (await _service.ListRowsAsync(propertyId, ct)).Select(RoomRowResponse.From).ToList()));

    [HttpPost]
    public async Task<IActionResult> Create(Guid propertyId, [FromBody] RoomRowRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created,
            new ApiResponse<RoomRowResponse>(RoomRowResponse.From(await _service.AddRowAsync(propertyId, request.ToInput(), ct))));

    [HttpPut("{rowId:guid}")]
    public async Task<IActionResult> Update(Guid propertyId, Guid rowId, [FromBody] UpdateRoomRowRequest request, CancellationToken ct) =>
        Ok(new ApiResponse<RoomRowResponse>(RoomRowResponse.From(
            await _service.UpdateRowAsync(propertyId, rowId, request.ToInput(), ct))));
}
