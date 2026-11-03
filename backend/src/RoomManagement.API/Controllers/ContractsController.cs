using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomManagement.API.Contracts.Common;
using RoomManagement.API.Contracts.Contracts;
using RoomManagement.Business.DTOs;
using RoomManagement.Business.Services;
using RoomManagement.Domain;
using RoomManagement.Domain.Enums;

namespace RoomManagement.API.Controllers;

[ApiController]
[Authorize(Roles = UserRoles.Landlord)]
[Route("api/properties/{propertyId:guid}/contracts")]
public sealed class ContractsController : ControllerBase
{
  private readonly ContractService _service;
  public ContractsController(ContractService service) => _service = service;

  [HttpPost]
  public async Task<IActionResult> Create(Guid propertyId,
      [FromBody] CreateContractRequest request, CancellationToken ct)
  {
    var result = ContractResponse.From(await _service.CreateAsync(propertyId, request.ToInput(), ct));
    return CreatedAtAction(nameof(Get), new { propertyId, contractId = result.Id },
        new ApiResponse<ContractResponse>(result));
  }

  [HttpGet]
  public async Task<IActionResult> List(Guid propertyId,
      [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
      [FromQuery] Guid? roomId = null, [FromQuery] ContractStatus? status = null,
      CancellationToken ct = default)
  {
    var result = await _service.ListAsync(propertyId,
        new ContractFilter(page, pageSize, roomId, status), ct);
    return Ok(new PageResponse<ContractResponse>(
        result.Items.Select(ContractResponse.From).ToList(),
        new PageMeta(page, pageSize, result.TotalItems,
            (int)Math.Ceiling((double)result.TotalItems / pageSize))));
  }

  [HttpGet("{contractId:guid}")]
  public async Task<IActionResult> Get(Guid propertyId, Guid contractId, CancellationToken ct) =>
      Ok(new ApiResponse<ContractResponse>(
          ContractResponse.From(await _service.GetAsync(propertyId, contractId, ct))));

  [HttpPost("{contractId:guid}/tenants")]
  public async Task<IActionResult> AddTenant(Guid propertyId, Guid contractId,
      [FromBody] AddContractTenantRequest request, CancellationToken ct) =>
      StatusCode(201, new ApiResponse<ContractTenantResponse>(ContractTenantResponse.From(
          await _service.AddTenantAsync(propertyId, contractId, request.ToInput(), ct))));

  [HttpPost("{contractId:guid}/activate")]
  public async Task<IActionResult> Activate(Guid propertyId, Guid contractId, CancellationToken ct) =>
      Ok(new ApiResponse<ContractResponse>(ContractResponse.From(
          await _service.ActivateAsync(propertyId, contractId, ct))));
}