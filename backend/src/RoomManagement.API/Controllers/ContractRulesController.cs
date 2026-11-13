using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomManagement.API.Contracts.Common;
using RoomManagement.Business.Services;
using RoomManagement.Domain;

namespace RoomManagement.API.Controllers;

[ApiController]
[Authorize(Roles = UserRoles.LandlordOrManager)]
[Route("api/properties/{propertyId:guid}/rules")]
public sealed class ContractRulesController(ContractRulesService service) : ControllerBase
{
    public sealed record SaveRulesRequest(string? Content, Guid? ExpectedVersionId);

    [HttpGet]
    public async Task<IActionResult> Get(Guid propertyId, CancellationToken ct)
    {
        var rules = await service.GetOwnedAsync(propertyId, ct);
        return Ok(new ApiResponse<object>(new { versionId = rules?.Id, versionNumber = rules?.VersionNumber,
            content = rules?.Content ?? ContractRulesService.DefaultTemplate, createdAt = rules?.CreatedAt, isPublished = rules is not null }));
    }

    [HttpPut]
    public async Task<IActionResult> Save(Guid propertyId, [FromBody] SaveRulesRequest request, CancellationToken ct)
    {
        var rules = await service.SaveAsync(propertyId, request.Content, request.ExpectedVersionId, ct);
        return Ok(new ApiResponse<object>(new { versionId = rules.Id, versionNumber = rules.VersionNumber,
            content = rules.Content, createdAt = rules.CreatedAt, isPublished = true }));
    }
}
