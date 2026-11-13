using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomManagement.API.Contracts.Common;
using RoomManagement.API.Contracts.Contracts;
using RoomManagement.API.Contracts.Billing;
using RoomManagement.Business.DTOs;
using RoomManagement.Business.Services;
using RoomManagement.Domain;
using RoomManagement.Domain.Entities;
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
    var created = await _service.CreateAsync(propertyId, request.ToInput(), ct);
    var result = ContractResponse.From(created.Contract, created.DepositInvoiceId);
    return CreatedAtAction(nameof(Get), new { propertyId, contractId = result.Id },
        new ApiResponse<ContractResponse>(result));
  }

  [HttpGet]
  public async Task<IActionResult> List(Guid propertyId,
      [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
      [FromQuery] Guid? roomId = null, [FromQuery] ContractStatus? status = null,
      CancellationToken ct = default)
  {
    var result = await _service.ListDetailsAsync(propertyId,
        new ContractFilter(page, pageSize, roomId, status), ct);
    return Ok(new PageResponse<ContractDetailResponse>(
        result.Items.Select(ContractDetailResponse.From).ToList(),
        new PageMeta(page, pageSize, result.TotalItems,
            (int)Math.Ceiling((double)result.TotalItems / pageSize))));
  }

  [HttpGet("{contractId:guid}")]
  public async Task<IActionResult> Get(Guid propertyId, Guid contractId, CancellationToken ct) =>
      Ok(new ApiResponse<ContractDetailResponse>(
          ContractDetailResponse.From(await _service.GetDetailAsync(propertyId, contractId, ct))));

  [HttpPost("{contractId:guid}/users")]
  public async Task<IActionResult> AddUser(Guid propertyId, Guid contractId,
      [FromBody] AddContractUserRequest request, CancellationToken ct) =>
      StatusCode(201, new ApiResponse<ContractTenantResponse>(ContractTenantResponse.From(
          await _service.AddUserAsync(propertyId, contractId, new(request.Email, request.JoinDate), ct))));

  [HttpPost("{contractId:guid}/tenants")]
  public async Task<IActionResult> AddTenant(Guid propertyId, Guid contractId,
      [FromBody] AddContractTenantRequest request, CancellationToken ct) =>
      StatusCode(201, new ApiResponse<ContractTenantResponse>(ContractTenantResponse.From(
          await _service.AddTenantAsync(propertyId, contractId, request.ToInput(), ct))));

  [HttpPut("{contractId:guid}/contact/{linkId:guid}")]
  public async Task<IActionResult> SetContact(Guid propertyId, Guid contractId, Guid linkId, CancellationToken ct) =>
      Ok(new ApiResponse<ContractTenantResponse>(ContractTenantResponse.From(
          await _service.SetContactAsync(propertyId, contractId, linkId, ct))));

  [HttpPost("{contractId:guid}/cancel")]
  public async Task<IActionResult> Cancel(Guid propertyId, Guid contractId, CancellationToken ct) =>
      Ok(new ApiResponse<ContractResponse>(ContractResponse.From(await _service.CancelAsync(propertyId, contractId, ct))));

  [HttpPost("{contractId:guid}/terminate")]
  public async Task<IActionResult> Terminate(Guid propertyId, Guid contractId,
      [FromBody] TerminateContractRequest request, CancellationToken ct) =>
      Ok(new ApiResponse<ContractResponse>(ContractResponse.From(
          await _service.TerminateAsync(propertyId, contractId, request.Reason, ct))));

  [HttpPost("{contractId:guid}/handover")]
  public async Task<IActionResult> Handover(Guid propertyId, Guid contractId,
      [FromBody] HandoverContractRequest request, CancellationToken ct) =>
      Ok(new ApiResponse<ContractResponse>(ContractResponse.From(
          await _service.HandoverAsync(propertyId, contractId, request.HandoverOn, ct))));

  [HttpPost("{contractId:guid}/meter-readings")]
  public async Task<IActionResult> RecordContractReading(Guid propertyId, Guid contractId,
      [FromBody] ContractMeterReadingRequest request, CancellationToken ct) =>
      StatusCode(201, new ApiResponse<MeterReadingResponse>(MeterReadingResponse.From(
          await _service.RecordContractReadingAsync(propertyId, contractId, request.Type,
              request.Year, request.Month, request.PreviousValue, request.CurrentValue, ct))));

  [HttpGet("{contractId:guid}/meter-readings")]
  public async Task<IActionResult> ListContractReadings(Guid propertyId, Guid contractId, CancellationToken ct) =>
      Ok(new ApiResponse<IReadOnlyList<MeterReadingResponse>>(
          (await _service.ListContractReadingsAsync(propertyId, contractId, ct))
              .Select(MeterReadingResponse.From).ToList()));

  [HttpPost("{contractId:guid}/complete-settlement")]
  public async Task<IActionResult> CompleteSettlement(Guid propertyId, Guid contractId,
      [FromBody] CompleteContractSettlementRequest request, CancellationToken ct) =>
      Ok(new ApiResponse<ContractResponse>(ContractResponse.From(
          await _service.CompleteSettlementAsync(propertyId, contractId,
              request.DepositDisposition, request.RefundAmount, request.Note, ct))));

  [HttpPost("{contractId:guid}/apply-deposit")]
  public async Task<IActionResult> ApplyDeposit(Guid propertyId, Guid contractId,
      [FromBody] ApplyDepositRequest request, CancellationToken ct) =>
      StatusCode(201, new ApiResponse<DepositApplication>(
          await _service.ApplyDepositAsync(propertyId, contractId, request.InvoiceId, request.Amount,
              request.Reason, request.IdempotencyKey, ct)));

  [HttpPut("{contractId:guid}/end-date")]
  public async Task<IActionResult> SetLegacyEndDate(Guid propertyId, Guid contractId,
      [FromBody] SetContractEndDateRequest request, CancellationToken ct) =>
      Ok(new ApiResponse<ContractResponse>(ContractResponse.From(
          await _service.SetLegacyEndDateAsync(propertyId, contractId, request.EndDate, ct))));

  [HttpPost("{contractId:guid}/tenants/{linkId:guid}/leave")]
  public async Task<IActionResult> Leave(Guid propertyId, Guid contractId, Guid linkId,
      [FromBody] LeaveContractRequest request, CancellationToken ct) =>
      Ok(new ApiResponse<ContractTenantResponse>(ContractTenantResponse.From(
          await _service.LeaveAsync(propertyId, contractId, linkId, request.LeaveDate, ct))));

  [HttpPost("{contractId:guid}/activate")]
  public async Task<IActionResult> Activate(Guid propertyId, Guid contractId, CancellationToken ct) =>
      Ok(new ApiResponse<ContractResponse>(ContractResponse.From(
          await _service.ActivateAsync(propertyId, contractId, ct))));

  [HttpPost("{contractId:guid}/tenants/{linkId:guid}/commitments")]
  public async Task<IActionResult> ConfirmCommitment(Guid propertyId, Guid contractId, Guid linkId,
      [FromBody] ConfirmMemberCommitmentRequest request, CancellationToken ct) =>
      StatusCode(201, new ApiResponse<MemberCommitmentResponse>(MemberCommitmentResponse.From(
          await _service.ConfirmCommitmentAsync(propertyId, contractId, linkId, request.ToInput(), ct))));

  [HttpGet("{contractId:guid}/tenants/{linkId:guid}/commitments")]
  public async Task<IActionResult> ListCommitments(Guid propertyId, Guid contractId, Guid linkId,
      CancellationToken ct) =>
      Ok(new ApiResponse<IReadOnlyList<MemberCommitmentResponse>>(
          (await _service.ListCommitmentsAsync(propertyId, contractId, linkId, ct))
              .Select(MemberCommitmentResponse.From).ToList()));

  [HttpGet("{contractId:guid}/tenants/{linkId:guid}/tenure")]
  public async Task<IActionResult> GetTenure(Guid propertyId, Guid contractId, Guid linkId,
      CancellationToken ct) =>
      Ok(new ApiResponse<IReadOnlyList<ContractTenureSpan>>(
          await _service.GetTenureAsync(propertyId, contractId, linkId, ct)));

  [HttpPost("{contractId:guid}/move-out-notices")]
  public async Task<IActionResult> CreateMoveOutNotice(Guid propertyId, Guid contractId,
      [FromBody] CreateMoveOutNoticeRequest request, CancellationToken ct) =>
      StatusCode(201, new ApiResponse<MoveOutNoticeResponse>(MoveOutNoticeResponse.From(
          await _service.CreateMoveOutNoticeAsync(propertyId, contractId, request.ToInput(), ct))));

  [HttpGet("{contractId:guid}/move-out-notices")]
  public async Task<IActionResult> ListMoveOutNotices(Guid propertyId, Guid contractId, CancellationToken ct) =>
      Ok(new ApiResponse<IReadOnlyList<MoveOutNoticeResponse>>(
          (await _service.ListMoveOutNoticesAsync(propertyId, contractId, ct))
              .Select(MoveOutNoticeResponse.From).ToList()));

  [HttpPost("{contractId:guid}/move-out-notices/{requestId:guid}/resolve")]
  public async Task<IActionResult> ResolveMoveOutNotice(Guid propertyId, Guid contractId, Guid requestId,
      [FromBody] ResolveMoveOutNoticeRequest request, CancellationToken ct) =>
      Ok(new ApiResponse<MoveOutNoticeResponse>(MoveOutNoticeResponse.From(
          await _service.ResolveMoveOutNoticeAsync(propertyId, contractId, requestId,
              request.ResolutionNote, ct))));
}
