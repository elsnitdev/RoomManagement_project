using RoomManagement.Domain.Enums;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Business.DTOs;

public sealed record CreateContractInput(
    Guid RoomId, string ContractCode, DateOnly StartDate, DateOnly? EndDate,
    decimal MonthlyRent, decimal DepositAmount, byte? PaymentDueDay, string? Note);

public sealed record AddContractTenantInput(Guid TenantId, DateOnly JoinDate);
public sealed record AddContractUserInput(string Email, DateOnly JoinDate);
public sealed record ContractTenantInfo(Guid Id, Guid TenantId, string? UserId, string FullName, string? Email, DateOnly JoinDate, DateOnly? LeaveDate);
public sealed record ContractDetail(Contract Contract, string RoomNumber, IReadOnlyList<ContractTenantInfo> Tenants,
    decimal OutstandingAmount, decimal DepositHeld, bool HasSettlement, bool HasDraftInvoices);

public sealed record ContractFilter(
    int Page, int PageSize, Guid? RoomId, ContractStatus? Status);
