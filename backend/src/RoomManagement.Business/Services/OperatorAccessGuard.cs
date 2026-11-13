using RoomManagement.Business.DTOs;
using RoomManagement.Business.Errors;
using RoomManagement.Business.Interfaces;

namespace RoomManagement.Business.Services;

/// <summary>
/// Bộ kiểm tra quyền và ngữ cảnh thao tác dùng chung cho Landlord và Manager.
/// Tách biệt ActorUserId (người thực hiện) và OwnerLandlordUserId (chủ trọ sở hữu tài nguyên).
/// </summary>
internal static class OperatorAccessGuard
{
    public static async Task<OperatorContext> RequireOperatorAsync(
        ICurrentLandlord current, ILandlordStore landlords, CancellationToken ct)
    {
        var actorId = current.GetUserId();
        if (string.IsNullOrWhiteSpace(actorId))
            throw new BusinessException(BusinessErrorType.InvalidOperation, "LANDLORD_UNAVAILABLE",
                "Tài khoản hiện tại không tồn tại hoặc đã ngừng hoạt động.");

        var context = await landlords.GetOperatorContextAsync(actorId, ct);
        if (context is null)
            throw new BusinessException(BusinessErrorType.InvalidOperation, "LANDLORD_UNAVAILABLE",
                "Tài khoản hiện tại không tồn tại, đã bị khóa hoặc chưa đổi mật khẩu tạm.");

        return context;
    }

    public static async Task<OperatorContext> RequireLandlordAsync(
        ICurrentLandlord current, ILandlordStore landlords, CancellationToken ct)
    {
        var context = await RequireOperatorAsync(current, landlords, ct);
        if (context.IsManager)
            throw new BusinessException(BusinessErrorType.Forbidden, "LANDLORD_ONLY",
                "Chỉ chủ trọ mới được phép thực hiện thao tác này.");

        return context;
    }

    public static async Task EnsurePropertyAccessAsync(
        OperatorContext context, Guid propertyId, ILandlordStore landlords, CancellationToken ct,
        string notFoundCode = "PROPERTY_NOT_FOUND", string notFoundMessage = "Không tìm thấy khu trọ.")
    {
        if (!await landlords.HasPropertyAccessAsync(context.ActorUserId, propertyId, ct))
            throw new BusinessException(BusinessErrorType.NotFound, notFoundCode, notFoundMessage);
    }

    public static async Task EnsureContractAccessAsync(
        OperatorContext context, Guid contractId, ILandlordStore landlords, CancellationToken ct,
        string notFoundCode = "CONTRACT_NOT_FOUND", string notFoundMessage = "Không tìm thấy hợp đồng.")
    {
        if (!await landlords.HasContractAccessAsync(context.ActorUserId, contractId, ct))
            throw new BusinessException(BusinessErrorType.NotFound, notFoundCode, notFoundMessage);
    }

    public static async Task EnsureInvoiceAccessAsync(
        OperatorContext context, Guid invoiceId, ILandlordStore landlords, CancellationToken ct,
        string notFoundCode = "INVOICE_NOT_FOUND", string notFoundMessage = "Không tìm thấy hóa đơn.")
    {
        if (!await landlords.HasInvoiceAccessAsync(context.ActorUserId, invoiceId, ct))
            throw new BusinessException(BusinessErrorType.NotFound, notFoundCode, notFoundMessage);
    }

    public static async Task EnsurePaymentAccessAsync(
        OperatorContext context, Guid paymentId, ILandlordStore landlords, CancellationToken ct,
        string notFoundCode = "PAYMENT_NOT_FOUND", string notFoundMessage = "Không tìm thấy khoản thanh toán.")
    {
        if (!await landlords.HasPaymentAccessAsync(context.ActorUserId, paymentId, ct))
            throw new BusinessException(BusinessErrorType.NotFound, notFoundCode, notFoundMessage);
    }
}
