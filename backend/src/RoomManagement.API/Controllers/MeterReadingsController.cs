using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomManagement.API.Contracts.Billing;
using RoomManagement.API.Contracts.Common;
using RoomManagement.Business.Services;
using RoomManagement.Domain;
using RoomManagement.Domain.Enums;

namespace RoomManagement.API.Controllers;

[ApiController]
[Authorize(Roles = UserRoles.Landlord)]
[Route("api/properties/{propertyId:guid}/rooms/{roomId:guid}/meter-readings")]
public sealed class MeterReadingsController : ControllerBase
{
    private readonly BillingInputService _service;
    public MeterReadingsController(BillingInputService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List(Guid propertyId, Guid roomId, [FromQuery] UtilityType? type, CancellationToken ct) =>
        Ok(new ApiResponse<IReadOnlyList<MeterReadingResponse>>(
            (await _service.ListReadingsAsync(propertyId, roomId, type, ct)).Select(MeterReadingResponse.From).ToList()));

    [HttpPost]
    public async Task<IActionResult> Create(Guid propertyId, Guid roomId, [FromBody] CreateMeterReadingRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, new ApiResponse<MeterReadingResponse>(
            MeterReadingResponse.From(await _service.CreateReadingAsync(propertyId, roomId, request.ToInput(), ct))));
}
