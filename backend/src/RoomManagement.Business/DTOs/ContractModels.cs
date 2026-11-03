using RoomManagement.Domain.Enums;

namespace RoomManagement.Business.DTOs;

public sealed record CreateContractInput(
    Guid RoomId, string ContractCode, DateOnly StartDate, DateOnly? EndDate,
    decimal MonthlyRent, decimal DepositAmount, byte? PaymentDueDay, string? Note);

public sealed record AddContractTenantInput(Guid TenantId, DateOnly JoinDate);

public sealed record ContractFilter(
    int Page, int PageSize, Guid? RoomId, ContractStatus? Status);