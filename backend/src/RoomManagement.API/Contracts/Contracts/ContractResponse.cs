using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;
using RoomManagement.Business.DTOs;

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

public sealed record ContractDetailResponse(Guid Id, string ContractCode, Guid RoomId, string RoomNumber,
    DateOnly StartDate, DateOnly? EndDate, DateOnly? ClosedOn, string? ClosureReason,
    decimal MonthlyRent, decimal DepositAmount, byte? PaymentDueDay, ContractStatus Status, string? Note,
    IReadOnlyList<ContractTenantInfo> Tenants, decimal OutstandingAmount, decimal DepositHeld, bool HasSettlement,
    bool HasDraftInvoices)
{
    public static ContractDetailResponse From(ContractDetail detail)
    {
        var c = detail.Contract;
        return new(c.Id, c.ContractCode, c.RoomId, detail.RoomNumber, c.StartDate, c.EndDate,
            c.ClosedOn, c.ClosureReason, c.MonthlyRent, c.DepositAmount, c.PaymentDueDay,
            c.Status, c.Note, detail.Tenants, detail.OutstandingAmount, detail.DepositHeld, detail.HasSettlement,
            detail.HasDraftInvoices);
    }
}
