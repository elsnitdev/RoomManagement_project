using RoomManagement.Business.DTOs;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Business.Interfaces;

public interface IPaymentStore
{
    /// <summary>Lưu khoản người thuê khai đã trả, sau khi kiểm tra quyền và khóa hóa đơn.</summary>
    Task<Payment> DeclareTenantAsync(Guid invoiceId, string tenantUserId,
        DeclareTenantPaymentInput input, CancellationToken ct);

    /// <summary>Lấy các khoản chính tài khoản người thuê đã khai trên hóa đơn được xem.</summary>
    Task<IReadOnlyList<TenantPaymentResult>> ListTenantAsync(Guid invoiceId,
        string tenantUserId, CancellationToken ct);

    /// <summary>Lấy toàn bộ khoản khai và khoản thu của hóa đơn thuộc chủ trọ.</summary>
    Task<IReadOnlyList<OwnerPaymentResult>> ListOwnedAsync(Guid invoiceId, string ownerId, CancellationToken ct);

    /// <summary>Xác nhận hoặc từ chối một khoản đang chờ trong giao dịch khóa hóa đơn.</summary>
    Task<OwnerPaymentResult> ReviewAsync(Guid paymentId, string ownerId,
        ReviewPaymentInput input, CancellationToken ct, string? actorUserId = null);

    /// <summary>Ghi ngay khoản tiền chủ trọ đã nhận, kèm lịch sử xác nhận.</summary>
    Task<OwnerPaymentResult> RecordReceivedAsync(Guid invoiceId, string ownerId,
        RecordReceivedPaymentInput input, CancellationToken ct, string? actorUserId = null);
    Task<Refund> RecordOverpaymentRefundAsync(Guid invoiceId, string ownerId,
        decimal amount, string? transactionCode, Guid idempotencyKey, CancellationToken ct, string? actorUserId = null);
    Task<IReadOnlyList<Refund>> ListOverpaymentRefundsAsync(Guid invoiceId, string ownerId, CancellationToken ct);
}
