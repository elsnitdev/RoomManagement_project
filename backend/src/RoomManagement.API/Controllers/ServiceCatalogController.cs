using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomManagement.API.Contracts.Billing;
using RoomManagement.API.Contracts.Common;
using RoomManagement.Business.Services;
using RoomManagement.Domain;

namespace RoomManagement.API.Controllers;

[ApiController]
[Authorize(Roles = UserRoles.Admin + "," + UserRoles.Landlord)]
[Route("api/services")]
public sealed class ServiceCatalogController : ControllerBase
{
    private readonly BillingInputService _service;
    public ServiceCatalogController(BillingInputService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(new ApiResponse<IReadOnlyList<ServiceResponse>>(
            (await _service.ListServicesAsync(ct)).Select(ServiceResponse.From).ToList()));

    [HttpPost]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<IActionResult> Create([FromBody] CreateServiceRequest request, CancellationToken ct)
    {
        var actorId = User.FindFirst("sub")?.Value ?? "";
        var result = await _service.CreateServiceAsync(actorId, request.ToInput(), ct);
        return StatusCode(StatusCodes.Status201Created,
            new ApiResponse<ServiceResponse>(ServiceResponse.From(result)));
    }
}
