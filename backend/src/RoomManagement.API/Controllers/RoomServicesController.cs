using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomManagement.API.Contracts.Billing;
using RoomManagement.API.Contracts.Common;
using RoomManagement.Business.Services;
using RoomManagement.Domain;

namespace RoomManagement.API.Controllers;

[ApiController]
[Authorize(Roles = UserRoles.LandlordOrManager)]
[Route("api/properties/{propertyId:guid}/rooms/{roomId:guid}/services")]
public sealed class RoomServicesController : ControllerBase
{
    private readonly BillingInputService _service;
    public RoomServicesController(BillingInputService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List(Guid propertyId, Guid roomId, CancellationToken ct) =>
        Ok(new ApiResponse<IReadOnlyList<RoomServiceResponse>>(
            (await _service.ListRoomServicesAsync(propertyId, roomId, ct)).Select(RoomServiceResponse.From).ToList()));

    [HttpPost]
    public async Task<IActionResult> Assign(Guid propertyId, Guid roomId,
        [FromBody] AssignRoomServiceRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, new ApiResponse<RoomServiceResponse>(
            RoomServiceResponse.From(await _service.AssignRoomServiceAsync(propertyId, roomId, request.ToInput(), ct))));

    [HttpPut("{assignmentId:guid}")]
    public async Task<IActionResult> Replace(Guid propertyId, Guid roomId, Guid assignmentId,
        [FromBody] AssignRoomServiceRequest request, CancellationToken ct) =>
        Ok(new ApiResponse<RoomServiceResponse>(RoomServiceResponse.From(
            await _service.ReplaceRoomServiceAsync(propertyId, roomId, assignmentId, request.ToInput(), ct))));
}
