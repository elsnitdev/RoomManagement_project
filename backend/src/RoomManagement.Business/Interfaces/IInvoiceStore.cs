using RoomManagement.Business.DTOs;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Business.Interfaces;

public interface IInvoiceStore
{
    Task<Contract?> GetOwnedContractAsync(Guid propertyId, Guid contractId, string ownerId, CancellationToken ct);
    Task<Contract?> GetOwnedContractByIdAsync(Guid contractId, string ownerId, CancellationToken ct);
    Task<(IReadOnlyList<InvoiceSummaryResult> Items, long Total)> ListOwnedAsync(
        Guid propertyId, string ownerId, InvoiceFilter filter, CancellationToken ct);
    Task<(IReadOnlyList<InvoiceSummaryResult> Items, long Total)> ListTenantAsync(
        string tenantUserId, InvoiceFilter filter, CancellationToken ct);
    Task<InvoiceDetailResult?> GetOwnedDetailAsync(Guid invoiceId, string ownerId, CancellationToken ct);
    Task<InvoiceDetailResult?> GetTenantDetailAsync(Guid invoiceId, string tenantUserId, CancellationToken ct);
    Task<Invoice> CreateDraftAsync(Invoice invoice, IReadOnlyList<InvoiceItem> items,
        string ownerId, CancellationToken ct);
    Task<Invoice> IssueAsync(Guid invoiceId, string ownerId, CancellationToken ct);
    Task<Invoice> CancelAsync(Guid invoiceId, string ownerId, CancellationToken ct);
}
