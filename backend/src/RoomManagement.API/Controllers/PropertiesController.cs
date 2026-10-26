using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomManagement.API.Contracts.Common;
using RoomManagement.API.Contracts.Properties;
using RoomManagement.Business.DTOs;
using RoomManagement.Business.Services;
using RoomManagement.Domain;

namespace RoomManagement.API.Controllers;

[ApiController]
[Authorize(Roles = UserRoles.Landlord)]
[Produces("application/json")]
[Route("api/properties")]
public sealed class PropertiesController : ControllerBase
{
    private readonly PropertyService _service;
    public PropertiesController(PropertyService service) => _service = service;

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<PropertyResponse>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreatePropertyRequest request, CancellationToken ct)
    {
        var result = PropertyResponse.From(await _service.CreateAsync(request.ToInput(), ct));
        return CreatedAtAction(nameof(Get), new { propertyId = result.Id }, new ApiResponse<PropertyResponse>(result));
    }

    [HttpGet]
    [ProducesResponseType(typeof(PageResponse<PropertyResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PageResponse<PropertyResponse>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, [FromQuery] bool? isActive = null,
        CancellationToken ct = default)
    {
        var result = await _service.ListAsync(new PropertyFilter(page, pageSize, search, isActive), ct);
        return Ok(new PageResponse<PropertyResponse>(result.Items.Select(PropertyResponse.From).ToList(),
            new PageMeta(page, pageSize, result.TotalItems, (int)Math.Ceiling((double)result.TotalItems / pageSize))));
    }

    [HttpGet("{propertyId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PropertyResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PropertyResponse>>> Get(Guid propertyId, CancellationToken ct) =>
        Ok(new ApiResponse<PropertyResponse>(PropertyResponse.From(await _service.GetAsync(propertyId, ct))));

    [HttpPut("{propertyId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PropertyResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PropertyResponse>>> Update(Guid propertyId, [FromBody] UpdatePropertyRequest request, CancellationToken ct) =>
        Ok(new ApiResponse<PropertyResponse>(PropertyResponse.From(await _service.UpdateAsync(propertyId, request.ToInput(), ct))));

    [HttpDelete("{propertyId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid propertyId, CancellationToken ct)
    {
        await _service.DeleteAsync(propertyId, ct);
        return NoContent();
    }
}
