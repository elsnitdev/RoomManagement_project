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
[Route("api/properties/{propertyId:guid}/utility-rates")]
public sealed class UtilityRatesController : ControllerBase
{
    private readonly BillingInputService _service;
    public UtilityRatesController(BillingInputService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List(Guid propertyId, [FromQuery] UtilityType? type, CancellationToken ct) =>
        Ok(new ApiResponse<IReadOnlyList<UtilityRateResponse>>(
            (await _service.ListRatesAsync(propertyId, type, ct)).Select(UtilityRateResponse.From).ToList()));

    [HttpPost]
    public async Task<IActionResult> Create(Guid propertyId, [FromBody] CreateUtilityRateRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, new ApiResponse<UtilityRateResponse>(
            UtilityRateResponse.From(await _service.CreateRateAsync(propertyId, request.ToInput(), ct))));
}
