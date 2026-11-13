using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;
using RoomManagement.Business.DTOs;

namespace RoomManagement.API.Contracts.Contracts;

public sealed record ContractResponse(
    Guid Id, string ContractCode, Guid RoomId, DateOnly StartDate, DateOnly? EndDate,
    decimal MonthlyRent, decimal DepositAmount, byte? PaymentDueDay,
    ContractStatus Status, string? Note, Guid? DepositInvoiceId = null, DateOnly? HandoverOn = null,
    ContractTenancyMode TenancyMode = ContractTenancyMode.CoTenancy)
{
  public static ContractResponse From(Contract c, Guid? depositInvoiceId = null) => new(c.Id, c.ContractCode, c.RoomId,
      c.StartDate, c.EndDate, c.MonthlyRent, c.DepositAmount,
      c.PaymentDueDay, c.Status, c.Note, depositInvoiceId, c.HandoverOn, c.TenancyMode);
}

public sealed record ContractTenantResponse(Guid Id, Guid ContractId, Guid TenantId, DateOnly JoinDate,
    DateOnly? LeaveDate, bool IsContactPerson, ContractTenantRole Role, DateTime? ResidenceApprovedAt,
    string? ResidenceApprovedByUserId)
{
  public static ContractTenantResponse From(ContractTenant tenant) =>
      new(tenant.Id, tenant.ContractId, tenant.TenantId, tenant.JoinDate,
          tenant.LeaveDate, tenant.IsContactPerson, tenant.Role, tenant.ResidenceApprovedAt,
          tenant.ResidenceApprovedByUserId);
}

public sealed record ContractDetailResponse(Guid Id, string ContractCode, Guid RoomId, string RoomNumber,
    DateOnly StartDate, DateOnly? EndDate, DateOnly? ClosedOn, string? ClosureReason,
    decimal MonthlyRent, decimal DepositAmount, byte? PaymentDueDay, ContractStatus Status, string? Note,
    IReadOnlyList<ContractTenantInfo> Tenants, decimal OutstandingAmount, decimal DepositHeld, bool HasSettlement,
    bool HasDraftInvoices, int MemberCount, int CurrentOccupantCount,
    IReadOnlyList<MoveOutNoticeResponse> MoveOutNotices, DateOnly? HandoverOn,
    ContractTenancyMode TenancyMode)
{
    public static ContractDetailResponse From(ContractDetail detail)
    {
        var c = detail.Contract;
        return new(c.Id, c.ContractCode, c.RoomId, detail.RoomNumber, c.StartDate, c.EndDate,
            c.ClosedOn, c.ClosureReason, c.MonthlyRent, c.DepositAmount, c.PaymentDueDay,
            c.Status, c.Note, detail.Tenants, detail.OutstandingAmount, detail.DepositHeld, detail.HasSettlement,
            detail.HasDraftInvoices,
            detail.Tenants.Count(t => t.LeaveDate == null ||
                t.LeaveDate >= RoomManagement.Business.Services.VietnamDate.Today),
            c.Status == ContractStatus.Active && c.StartDate <= RoomManagement.Business.Services.VietnamDate.Today &&
            (c.EndDate == null || c.EndDate >= RoomManagement.Business.Services.VietnamDate.Today)
                ? detail.Tenants.Count(t => t.JoinDate <= RoomManagement.Business.Services.VietnamDate.Today &&
                    (t.LeaveDate == null || t.LeaveDate >= RoomManagement.Business.Services.VietnamDate.Today) &&
                    (t.Role == ContractTenantRole.AuthorizedOccupant && t.ResidenceApprovedAt != null ||
                     t.HasConfirmedCommitment ||
                     t.EvidenceStatus == CommitmentEvidenceStatus.LegacyPendingEvidence)) : 0,
            (detail.MoveOutNotices ?? []).Select(MoveOutNoticeResponse.From).ToList(), c.HandoverOn,
            c.TenancyMode);
    }
}

public sealed record MemberCommitmentResponse(Guid Id, Guid ContractTenantId,
    CommitmentType CommitmentType, DateOnly? SignedOn, string? SignerName,
    Guid? SignerTenantProfileId, string? SignerRelation, string? TermsSnapshot,
    DateTime? ConfirmedAt, string? ConfirmedByUserId, CommitmentEvidenceStatus EvidenceStatus,
    DateTime? SupersededAt, DateTime CreatedAt)
{
    public static MemberCommitmentResponse From(MemberCommitment c) =>
        new(c.Id, c.ContractTenantId, c.CommitmentType, c.SignedOn, c.SignerName,
            c.SignerTenantProfileId, c.SignerRelation, c.TermsSnapshot,
            c.ConfirmedAt, c.ConfirmedByUserId, c.EvidenceStatus, c.SupersededAt, c.CreatedAt);
}

public sealed record MoveOutNoticeResponse(Guid Id, Guid ContractId, Guid RequestedByTenantId,
    TenantRequestCategory Category, string Title, string Description, TenantRequestStatus Status,
    DateTime RequestedAt, DateTime? ResolvedAt, string? ResolutionNote)
{
    public static MoveOutNoticeResponse From(TenantRequest request) =>
        new(request.Id, request.ContractId, request.RequestedByTenantId, request.Category,
            request.Title, request.Description, request.Status, request.RequestedAt,
            request.ResolvedAt, request.ResolutionNote);
}
