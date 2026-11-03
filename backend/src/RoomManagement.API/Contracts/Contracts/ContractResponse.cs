using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.API.Contracts.Contracts;

public sealed record ContractResponse(
    Guid Id, string ContractCode, Guid RoomId, DateOnly StartDate, DateOnly? EndDate,
    decimal MonthlyRent, decimal DepositAmount, byte? PaymentDueDay,
    ContractStatus Status, string? Note)
{
  public static ContractResponse From(Contract c) => new(c.Id, c.ContractCode, c.RoomId,
      c.StartDate, c.EndDate, c.MonthlyRent, c.DepositAmount,
      c.PaymentDueDay, c.Status, c.Note);
}

public sealed record ContractTenantResponse(Guid Id, Guid ContractId, Guid TenantId, DateOnly JoinDate)
{
  public static ContractTenantResponse From(ContractTenant tenant) =>
      new(tenant.Id, tenant.ContractId, tenant.TenantId, tenant.JoinDate);
}
