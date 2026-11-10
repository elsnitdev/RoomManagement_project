using RoomManagement.Business.DTOs;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Business.Interfaces;

public interface IInvoiceStore
{
    /// <summary>Tìm hợp đồng trong khu trọ đang hoạt động của chủ trọ.</summary>
    Task<Contract?> GetOwnedContractAsync(Guid propertyId, Guid contractId, string ownerId, CancellationToken ct);
    /// <summary>Tìm hợp đồng thuộc chủ trọ theo mã hợp đồng.</summary>
    Task<Contract?> GetOwnedContractByIdAsync(Guid contractId, string ownerId, CancellationToken ct);
    Task<Invoice?> GetOwnedDepositAsync(Guid contractId, string ownerId, CancellationToken ct);
    /// <summary>Lấy danh sách hóa đơn của chủ trọ theo bộ lọc và phân trang.</summary>
    Task<(IReadOnlyList<InvoiceSummaryResult> Items, long Total)> ListOwnedAsync(
        Guid propertyId, string ownerId, InvoiceFilter filter, CancellationToken ct);
    /// <summary>Lấy danh sách hóa đơn được phát hành cho người thuê theo bộ lọc.</summary>
    Task<(IReadOnlyList<InvoiceSummaryResult> Items, long Total)> ListTenantAsync(
        string tenantUserId, InvoiceFilter filter, CancellationToken ct);
    /// <summary>Lấy chi tiết hóa đơn thuộc quyền sở hữu của chủ trọ.</summary>
    Task<InvoiceDetailResult?> GetOwnedDetailAsync(Guid invoiceId, string ownerId, CancellationToken ct);
    /// <summary>Lấy chi tiết hóa đơn mà người thuê được xem.</summary>
    Task<InvoiceDetailResult?> GetTenantDetailAsync(Guid invoiceId, string tenantUserId, CancellationToken ct);
    Task<Property?> GetTenantPaymentPropertyAsync(Guid invoiceId, string tenantUserId, CancellationToken ct);
    Task<bool> CanTenantDeclarePaymentAsync(Guid invoiceId, string tenantUserId, DateOnly today, CancellationToken ct);
    /// <summary>Tạo hóa đơn nháp cùng các dòng tiền sau khi kiểm tra dữ liệu.</summary>
    Task<Invoice> CreateDraftAsync(Invoice invoice, IReadOnlyList<InvoiceItem> items,
        string ownerId, CancellationToken ct);
    /// <summary>Phát hành hóa đơn nháp sau khi kiểm tra điều kiện phát hành.</summary>
    Task<Invoice> IssueAsync(Guid invoiceId, string ownerId, CancellationToken ct);
    /// <summary>Hủy hóa đơn nếu chưa phát sinh giao dịch tài chính.</summary>
    Task<Invoice> CancelAsync(Guid invoiceId, string ownerId, CancellationToken ct);
}
