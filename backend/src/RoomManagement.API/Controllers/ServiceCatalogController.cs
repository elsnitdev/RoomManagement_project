using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomManagement.API.Contracts.Billing;
using RoomManagement.API.Contracts.Common;
using RoomManagement.Business.Services;
using RoomManagement.Domain;

namespace RoomManagement.API.Controllers;

[ApiController]
[Authorize(Roles = UserRoles.Landlord)]
[Route("api/properties/{propertyId:guid}/services")]
public sealed class ServiceCatalogController : ControllerBase
{
    private readonly BillingInputService _service;
    public ServiceCatalogController(BillingInputService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List(Guid propertyId, CancellationToken ct) =>
        Ok(new ApiResponse<IReadOnlyList<ServiceResponse>>(
            (await _service.ListServicesAsync(propertyId, ct)).Select(ServiceResponse.From).ToList()));

    [HttpPost]
    public async Task<IActionResult> Create(Guid propertyId, [FromBody] CreateServiceRequest request, CancellationToken ct)
    {
        var result = await _service.CreateServiceAsync(propertyId, request.ToInput(), ct);
        return StatusCode(StatusCodes.Status201Created,
            new ApiResponse<ServiceResponse>(ServiceResponse.From(result)));
    }

    [HttpPut("{serviceId:guid}")]
    public async Task<IActionResult> Update(Guid propertyId, Guid serviceId, [FromBody] UpdateServiceRequest request, CancellationToken ct) =>
        Ok(new ApiResponse<ServiceResponse>(ServiceResponse.From(
            await _service.UpdateServiceAsync(propertyId, serviceId, request.ToInput(), ct))));
}
