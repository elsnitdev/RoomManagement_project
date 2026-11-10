using RoomManagement.Domain.Entities;

namespace RoomManagement.Business.Interfaces;

public interface IPaymentEvidenceStore
{
    Task<bool> OwnsConfirmedPaymentAsync(Guid paymentId, string ownerId, CancellationToken ct);
    Task<IReadOnlyList<PaymentEvidence>> ListAsync(Guid paymentId, CancellationToken ct);
    Task<PaymentEvidence?> GetAsync(Guid id, CancellationToken ct);
    Task<PaymentEvidence> AddOrGetAsync(PaymentEvidence evidence, CancellationToken ct);
}
