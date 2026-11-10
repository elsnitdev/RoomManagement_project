using RoomManagement.Domain.Entities;

namespace RoomManagement.Business.DTOs;

public sealed record ContractRulesTenantView(Guid ContractId, string ContractCode, Guid ContractTenantId,
    string TenantName, ContractRulesVersion? Rules, bool AcceptedCurrentVersion, DateTime? AcceptedAt);
