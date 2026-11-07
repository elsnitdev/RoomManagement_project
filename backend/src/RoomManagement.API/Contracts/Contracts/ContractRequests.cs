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
    [StringLength(1000)] string? Note)
{
  public CreateContractInput ToInput() => new(RoomId, ContractCode, StartDate,
      EndDate, MonthlyRent, DepositAmount, PaymentDueDay, Note);
}

public sealed record AddContractTenantRequest(
    [property: JsonRequired] Guid TenantId,
    [property: JsonRequired] DateOnly JoinDate,
    bool IsContactPerson = false)
{
  public AddContractTenantInput ToInput() => new(TenantId, JoinDate, IsContactPerson);
}

public sealed record AddContractUserRequest([property: Required, EmailAddress] string Email,
    [property: JsonRequired] DateOnly JoinDate);
public sealed record LeaveContractRequest([property: JsonRequired] DateOnly LeaveDate);
public sealed record TerminateContractRequest([property: Required, StringLength(1000)] string Reason);
public sealed record SetContractEndDateRequest([property: JsonRequired] DateOnly EndDate);

public sealed record ConfirmMemberCommitmentRequest(
    [property: JsonRequired] CommitmentType CommitmentType,
    [property: JsonRequired] DateOnly SignedOn,
    [property: Required, StringLength(200)] string SignerName,
    Guid? SignerTenantProfileId,
    [StringLength(100)] string? SignerRelation,
    [property: Required] string TermsSnapshot)
{
    public ConfirmMemberCommitmentInput ToInput() => new(CommitmentType, SignedOn,
        SignerName, SignerTenantProfileId, SignerRelation, TermsSnapshot);
}

public sealed record CreateMoveOutNoticeRequest(
    [property: JsonRequired] Guid TenantId,
    [property: Required, StringLength(200)] string Title,
    [property: Required, StringLength(2000)] string Description)
{
    public CreateMoveOutNoticeInput ToInput() => new(TenantId, Title, Description);
}

public sealed record ResolveMoveOutNoticeRequest(
    [property: Required, StringLength(1000)] string ResolutionNote);
