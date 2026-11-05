using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomManagement.API.Contracts.Common;
using RoomManagement.API.Contracts.Contracts;
using RoomManagement.Business.Services;
using RoomManagement.Domain;

namespace RoomManagement.API.Controllers;

[ApiController]
[Authorize(Roles = UserRoles.Tenant)]
[Route("api/me/contracts")]
public sealed class MyContractsController : ControllerBase
{
    private readonly ContractService _service;
    public MyContractsController(ContractService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _service.ListMineAsync(User.FindFirst("sub")?.Value ?? "", page, pageSize, ct);
        return Ok(new PageResponse<ContractDetailResponse>(result.Items.Select(ContractDetailResponse.From).ToList(),
            new PageMeta(page, pageSize, result.TotalItems, (int)Math.Ceiling((double)result.TotalItems / pageSize))));
    }

    [HttpGet("{contractId:guid}")]
    public async Task<IActionResult> Get(Guid contractId, CancellationToken ct) =>
        Ok(new ApiResponse<ContractDetailResponse>(ContractDetailResponse.From(
            await _service.GetMineAsync(contractId, User.FindFirst("sub")?.Value ?? "", ct))));
}
