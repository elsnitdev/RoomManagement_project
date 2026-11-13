using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomManagement.API.Contracts.Common;
using RoomManagement.API.Contracts.Managers;
using RoomManagement.Business.DTOs.Managers;
using RoomManagement.Business.Interfaces;
using RoomManagement.Domain;

namespace RoomManagement.API.Controllers;

[ApiController]
[Authorize(Roles = UserRoles.Landlord)]
[Produces("application/json")]
[Route("api/landlord/managers")]
public sealed class LandlordManagersController : ControllerBase
{
    private readonly IManagerService _service;

    public LandlordManagersController(IManagerService service) => _service = service;

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ManagerResponse>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateManagerRequest request, CancellationToken ct)
    {
        var result = ManagerResponse.From(await _service.CreateAsync(request.ToInput(), ct));
        return CreatedAtAction(nameof(Get), new { managerId = result.Id }, new ApiResponse<ManagerResponse>(result));
    }

    [HttpGet]
    [ProducesResponseType(typeof(PageResponse<ManagerResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PageResponse<ManagerResponse>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] bool? isActive = null,
        CancellationToken ct = default)
    {
        var result = await _service.ListAsync(new ManagerFilter(page, pageSize, search, isActive), ct);
        return Ok(new PageResponse<ManagerResponse>(
            result.Items.Select(ManagerResponse.From).ToList(),
            new PageMeta(page, pageSize, result.TotalItems, (int)Math.Ceiling((double)result.TotalItems / pageSize))));
    }

    [HttpGet("{managerId}")]
    [ProducesResponseType(typeof(ApiResponse<ManagerResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ManagerResponse>>> Get(string managerId, CancellationToken ct) =>
        Ok(new ApiResponse<ManagerResponse>(ManagerResponse.From(await _service.GetAsync(managerId, ct))));

    [HttpPost("{managerId}/properties")]
    [ProducesResponseType(typeof(ApiResponse<ManagerResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ManagerResponse>>> AssignProperties(
        string managerId,
        [FromBody] AssignManagerPropertiesRequest request,
        CancellationToken ct) =>
        Ok(new ApiResponse<ManagerResponse>(ManagerResponse.From(
            await _service.AssignPropertiesAsync(managerId, request.PropertyIds, ct))));

    [HttpDelete("{managerId}/properties/{propertyId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RevokeProperty(string managerId, Guid propertyId, CancellationToken ct)
    {
        await _service.RevokePropertyAsync(managerId, propertyId, ct);
        return NoContent();
    }

    [HttpPost("{managerId}/lock")]
    [ProducesResponseType(typeof(ApiResponse<ManagerResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ManagerResponse>>> Lock(string managerId, CancellationToken ct) =>
        Ok(new ApiResponse<ManagerResponse>(ManagerResponse.From(await _service.SetLockStateAsync(managerId, false, ct))));

    [HttpPost("{managerId}/unlock")]
    [ProducesResponseType(typeof(ApiResponse<ManagerResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ManagerResponse>>> Unlock(string managerId, CancellationToken ct) =>
        Ok(new ApiResponse<ManagerResponse>(ManagerResponse.From(await _service.SetLockStateAsync(managerId, true, ct))));

    [HttpDelete("{managerId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(string managerId, CancellationToken ct)
    {
        await _service.DeleteAsync(managerId, ct);
        return NoContent();
    }
}
