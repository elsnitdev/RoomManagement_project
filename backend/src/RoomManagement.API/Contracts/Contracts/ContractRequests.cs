using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using RoomManagement.Business.DTOs;
using RoomManagement.Domain.Enums;

namespace RoomManagement.API.Contracts.Contracts;

public sealed record CreateContractRequest(
    [property: JsonRequired] Guid RoomId,
    [property: JsonRequired][Required, StringLength(50)] string ContractCode,
    [property: JsonRequired] DateOnly StartDate,
    DateOnly? EndDate,
    [property: JsonRequired] decimal MonthlyRent,
    [property: JsonRequired] decimal DepositAmount,
    byte? PaymentDueDay,
    [StringLength(1000)] string? Note,
    InitialDepositPaymentStatus DepositPaymentStatus = InitialDepositPaymentStatus.Unpaid,
    PaymentMethod? DepositPaymentMethod = null,
    DateTimeOffset? DepositPaidAt = null,
    ContractTenancyMode TenancyMode = ContractTenancyMode.CoTenancy,
    IReadOnlyList<InitialRoomServiceInput>? RoomServices = null)
{
  public CreateContractInput ToInput() => new(RoomId, ContractCode, StartDate,
      EndDate, MonthlyRent, DepositAmount, PaymentDueDay, Note,
      DepositPaymentStatus, DepositPaymentMethod, DepositPaidAt, TenancyMode, RoomServices);
}

public sealed record AddContractTenantRequest(
    [property: JsonRequired] Guid TenantId,
    [property: JsonRequired] DateOnly JoinDate,
    bool IsContactPerson = false,
    ContractTenantRole Role = ContractTenantRole.ContractParty)
{
  public AddContractTenantInput ToInput() => new(TenantId, JoinDate, IsContactPerson, Role);
}

public sealed record AddContractUserRequest([Required, EmailAddress] string Email,
    [property: JsonRequired] DateOnly JoinDate);
public sealed record LeaveContractRequest([property: JsonRequired] DateOnly LeaveDate);
public sealed record TerminateContractRequest([Required, StringLength(1000)] string Reason);
public sealed record HandoverContractRequest([property: JsonRequired] DateOnly HandoverOn);
public sealed record ContractMeterReadingRequest([property: JsonRequired] UtilityType Type,
    [property: JsonRequired] short Year, [property: JsonRequired] byte Month,
    [property: JsonRequired] decimal PreviousValue, [property: JsonRequired] decimal CurrentValue);
public sealed record CompleteContractSettlementRequest(
    [Required] string DepositDisposition,
    [property: JsonRequired] decimal RefundAmount,
    [StringLength(1000)] string? Note);
public sealed record ApplyDepositRequest([property: JsonRequired] Guid InvoiceId,
    [property: JsonRequired] decimal Amount, [Required] string Reason,
    [Required] string IdempotencyKey);
public sealed record SetContractEndDateRequest([property: JsonRequired] DateOnly EndDate);

public sealed record ConfirmMemberCommitmentRequest(
    [property: JsonRequired] CommitmentType CommitmentType,
    [property: JsonRequired] DateOnly SignedOn,
    [Required, StringLength(200)] string SignerName,
    Guid? SignerTenantProfileId,
    [StringLength(100)] string? SignerRelation,
    [Required] string TermsSnapshot)
{
    public ConfirmMemberCommitmentInput ToInput() => new(CommitmentType, SignedOn,
        SignerName, SignerTenantProfileId, SignerRelation, TermsSnapshot);
}

public sealed record CreateMoveOutNoticeRequest(
    [property: JsonRequired] Guid TenantId,
    [Required, StringLength(200)] string Title,
    [Required, StringLength(2000)] string Description)
{
    public CreateMoveOutNoticeInput ToInput() => new(TenantId, Title, Description);
}

public sealed record ResolveMoveOutNoticeRequest(
    [Required, StringLength(1000)] string ResolutionNote);
