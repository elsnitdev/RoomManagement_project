using RoomManagement.Business.DTOs;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Business.Interfaces;

public interface IPaymentStore
{
    /// <summary>Lưu khoản người thuê khai đã trả, sau khi kiểm tra quyền và khóa hóa đơn.</summary>
    Task<Payment> DeclareTenantAsync(Guid invoiceId, string tenantUserId,
        DeclareTenantPaymentInput input, CancellationToken ct);

    /// <summary>Lấy các khoản chính tài khoản người thuê đã khai trên hóa đơn được xem.</summary>
    Task<IReadOnlyList<Payment>> ListTenantAsync(Guid invoiceId, string tenantUserId, CancellationToken ct);
}
