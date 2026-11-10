using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomManagement.API.Contracts.Common;
using RoomManagement.Business.Services;
using RoomManagement.Domain;

namespace RoomManagement.API.Controllers;

[ApiController]
[Authorize(Roles = UserRoles.Tenant)]
[Route("api/me/contract-rules")]
public sealed class MyContractRulesController(ContractRulesService service) : ControllerBase
{
    public sealed record AcceptRequest(Guid VersionId);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var rows = await service.ListMineAsync(User.FindFirst("sub")?.Value ?? "", ct);
        return Ok(new ApiResponse<object>(rows.Select(x => new { x.ContractId, x.ContractCode, x.ContractTenantId,
            x.TenantName, versionId = x.Rules?.Id, versionNumber = x.Rules?.VersionNumber,
            content = x.Rules?.Content, x.AcceptedCurrentVersion, x.AcceptedAt }).ToList()));
    }

    [HttpPost("{contractId:guid}/tenants/{linkId:guid}/accept")]
    public async Task<IActionResult> Accept(Guid contractId, Guid linkId, [FromBody] AcceptRequest request, CancellationToken ct)
    {
        var result = await service.AcceptAsync(contractId, linkId, request.VersionId, User.FindFirst("sub")?.Value ?? "", ct);
        return Ok(new ApiResponse<object>(new { result.Id, result.RulesVersionId, result.ContractTenantId, result.AcceptedAt }));
    }
}
