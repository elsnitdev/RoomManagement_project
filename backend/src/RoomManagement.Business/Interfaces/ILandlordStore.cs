using RoomManagement.Business.DTOs;
using RoomManagement.Domain;

namespace RoomManagement.Business.Interfaces;

public interface ILandlordStore
{
    /// <summary>Kiểm tra tài khoản có phải chủ trọ đang hoạt động hay không.</summary>
    Task<bool> IsActiveLandlordAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// Lấy ngữ cảnh thao tác hiện tại (Landlord hoặc Manager đang hoạt động và đã đổi mật khẩu tạm).
    /// </summary>
    async Task<OperatorContext?> GetOperatorContextAsync(string userId, CancellationToken cancellationToken)
    {
        return await IsActiveLandlordAsync(userId, cancellationToken)
            ? new OperatorContext(userId, userId, UserRoles.Landlord, IsManager: false)
            : null;
    }

    /// <summary>Kiểm tra actor hiện tại có quyền thao tác với khu trọ hay không.</summary>
    Task<bool> HasPropertyAccessAsync(string actorUserId, Guid propertyId, CancellationToken cancellationToken)
        => Task.FromResult(true);

    /// <summary>Kiểm tra actor hiện tại có quyền thao tác với hợp đồng (truy ngược về Property) hay không.</summary>
    Task<bool> HasContractAccessAsync(string actorUserId, Guid contractId, CancellationToken cancellationToken)
        => Task.FromResult(true);

    /// <summary>Kiểm tra actor hiện tại có quyền thao tác với hóa đơn (truy ngược về Property) hay không.</summary>
    Task<bool> HasInvoiceAccessAsync(string actorUserId, Guid invoiceId, CancellationToken cancellationToken)
        => Task.FromResult(true);

    /// <summary>Kiểm tra actor hiện tại có quyền thao tác với thanh toán (truy ngược về Property) hay không.</summary>
    Task<bool> HasPaymentAccessAsync(string actorUserId, Guid paymentId, CancellationToken cancellationToken)
        => Task.FromResult(true);
}
