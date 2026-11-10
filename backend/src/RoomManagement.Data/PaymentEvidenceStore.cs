using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RoomManagement.Business.Interfaces;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Data;

public sealed class PaymentEvidenceStore(RoomManagementDbContext db) : IPaymentEvidenceStore
{
    public Task<bool> OwnsConfirmedPaymentAsync(Guid paymentId, string ownerId, CancellationToken ct) =>
        (from payment in db.Payments.AsNoTracking()
         join invoice in InvoiceAccessQueries.OwnedInvoices(db, ownerId).AsNoTracking() on payment.InvoiceId equals invoice.Id
         where payment.Id == paymentId && payment.Status == PaymentStatus.Success
         select payment.Id).AnyAsync(ct);

    public async Task<IReadOnlyList<PaymentEvidence>> ListAsync(Guid paymentId, CancellationToken ct) =>
        await db.PaymentEvidence.AsNoTracking().Where(x => x.PaymentId == paymentId)
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(ct);

    public Task<PaymentEvidence?> GetAsync(Guid id, CancellationToken ct) =>
        db.PaymentEvidence.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);

    public async Task<PaymentEvidence> AddOrGetAsync(PaymentEvidence evidence, CancellationToken ct)
    {
        db.PaymentEvidence.Add(evidence);
        try { await db.SaveChangesAsync(ct); return evidence; }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            db.Entry(evidence).State = EntityState.Detached;
            var existing = await GetAsync(evidence.Id, ct);
            if (existing is null) throw;
            return existing;
        }
    }
}
