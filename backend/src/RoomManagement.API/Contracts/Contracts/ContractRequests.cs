using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using RoomManagement.Business.DTOs;

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
    [property: JsonRequired] DateOnly JoinDate)
{
  public AddContractTenantInput ToInput() => new(TenantId, JoinDate);
}

public sealed record AddContractUserRequest([property: Required, EmailAddress] string Email,
    [property: JsonRequired] DateOnly JoinDate);
public sealed record LeaveContractRequest([property: JsonRequired] DateOnly LeaveDate);
public sealed record TerminateContractRequest([property: Required, StringLength(1000)] string Reason);
