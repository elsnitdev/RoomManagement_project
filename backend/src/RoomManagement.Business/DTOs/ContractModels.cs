using RoomManagement.Domain.Enums;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Business.DTOs;

public sealed record CreateContractInput(
    Guid RoomId, string ContractCode, DateOnly StartDate, DateOnly? EndDate,
    decimal MonthlyRent, decimal DepositAmount, byte? PaymentDueDay, string? Note,
    InitialDepositPaymentStatus DepositPaymentStatus = InitialDepositPaymentStatus.Unpaid,
    PaymentMethod? DepositPaymentMethod = null, DateTimeOffset? DepositPaidAt = null);

public enum InitialDepositPaymentStatus { Unpaid = 1, Paid = 2 }

public sealed record CreateContractResult(Contract Contract, Guid? DepositInvoiceId);

public sealed record ContractCreation(Contract Contract, Invoice? DepositInvoice,
    InvoiceItem? DepositItem, Payment? InitialPayment, PaymentReviewEvent? InitialPaymentReview);

public sealed record AddContractTenantInput(Guid TenantId, DateOnly JoinDate, bool IsContactPerson);
public sealed record AddContractUserInput(string Email, DateOnly JoinDate);
public sealed record ContractTenantInfo(Guid Id, Guid TenantId, string? UserId, string FullName,
    string? PhoneNumber, string? Email, DateOnly JoinDate, DateOnly? LeaveDate, bool IsContactPerson)
{
    public Guid? CurrentCommitmentId { get; init; }
    public DateOnly? CommitmentSignedOn { get; init; }
    public DateTime? CommitmentConfirmedAt { get; init; }
    public CommitmentEvidenceStatus? EvidenceStatus { get; init; }
    public bool HasConfirmedCommitment { get; init; }
    public IReadOnlyList<ContractTenureSpan> TenureSpans { get; init; } = [];
}
public sealed record ContractDetail(Contract Contract, string RoomNumber, IReadOnlyList<ContractTenantInfo> Tenants,
    decimal OutstandingAmount, decimal DepositHeld, bool HasSettlement, bool HasDraftInvoices,
    IReadOnlyList<TenantRequest>? MoveOutNotices = null);

public sealed record ContractFilter(
    int Page, int PageSize, Guid? RoomId, ContractStatus? Status);

/// <summary>Dữ liệu chủ trọ xác nhận đã nhận bản cam kết ký ngoài hệ thống.</summary>
public sealed record ConfirmMemberCommitmentInput(CommitmentType CommitmentType, DateOnly SignedOn,
    string SignerName, Guid? SignerTenantProfileId, string? SignerRelation, string TermsSnapshot);

/// <summary>Thông tin gửi thông báo rời trọ thay cho profile người thuê.</summary>
public sealed record CreateMoveOutNoticeInput(Guid TenantId, string Title, string Description);

/// <summary>Một khoảng người thuê được ghi trong hợp đồng đã từng kích hoạt.</summary>
public sealed record ContractTenurePeriod(DateOnly From, DateOnly To);

/// <summary>Một chuỗi cư trú liên tiếp, có thể bao gồm khoảng trống tối đa bảy ngày.</summary>
public sealed record ContractTenureSpan(DateOnly From, DateOnly To, int CalendarDays,
    int ContractDays);
