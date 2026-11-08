using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RoomManagement.Business.DTOs;
using RoomManagement.Business.Errors;
using RoomManagement.Business.Interfaces;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Data;

public sealed class PaymentStore : IPaymentStore
{
    private readonly RoomManagementDbContext _db;

    public PaymentStore(RoomManagementDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<Payment> DeclareTenantAsync(Guid invoiceId, string tenantUserId,
        DeclareTenantPaymentInput input, CancellationToken ct)
    {
        if (!await InvoiceAccessQueries.TenantInvoices(_db, tenantUserId)
                .AnyAsync(x => x.Id == invoiceId, ct))
            throw NotFound();

        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await InvoiceTransactionLock.AcquireAsync(_db, invoiceId, ct);
        var invoice = await InvoiceAccessQueries.TenantInvoices(_db, tenantUserId)
            .AsNoTracking().SingleOrDefaultAsync(x => x.Id == invoiceId, ct)
            ?? throw NotFound();
        var key = $"tenant:{input.IdempotencyKey:N}";
        var existing = await _db.Payments.AsNoTracking()
            .SingleOrDefaultAsync(x => x.IdempotencyKey == key, ct);
        if (existing is not null)
        {
            if (existing.InvoiceId != invoiceId || existing.SubmittedByUserId != tenantUserId ||
                existing.ClaimedAmount != input.Amount || existing.PaymentMethod != input.Method ||
                existing.PaidAt != input.PaidAt.UtcDateTime ||
                existing.TransactionCode != input.TransactionCode || existing.Note != input.Note)
                throw new BusinessException(BusinessErrorType.Conflict,
                    "PAYMENT_IDEMPOTENCY_CONFLICT", "Khóa gửi lại đã được dùng cho khoản khác.");
            return existing;
        }

        var today = RoomManagement.Business.Services.VietnamDate.Today;
        var stillLinked = await _db.ContractTenants.AnyAsync(link =>
            link.ContractId == invoice.ContractId &&
            (link.LeaveDate == null || link.LeaveDate >= today) &&
            _db.TenantProfiles.Any(profile => profile.Id == link.TenantId &&
                profile.UserId == tenantUserId), ct);
        if (!stillLinked)
            throw new BusinessException(BusinessErrorType.InvalidOperation,
                "PAYMENT_MEMBERSHIP_ENDED", "Bạn đã rời hợp đồng và không thể khai khoản thanh toán mới.");

        var remaining = await _db.InvoiceBalances.AsNoTracking()
            .Where(x => x.InvoiceId == invoice.Id).Select(x => x.RemainingAmount)
            .SingleAsync(ct);
        if (remaining <= 0)
            throw new BusinessException(BusinessErrorType.InvalidOperation,
                "INVOICE_ALREADY_PAID", "Hóa đơn không còn số tiền phải thu.");

        var payment = new Payment
        {
            Id = Guid.NewGuid(), InvoiceId = invoiceId, SubmittedByUserId = tenantUserId,
            ClaimedAmount = input.Amount, PaymentMethod = input.Method,
            Status = PaymentStatus.Pending, PaidAt = input.PaidAt.UtcDateTime,
            TransactionCode = input.TransactionCode, IdempotencyKey = key,
            Note = input.Note, CreatedAt = DateTime.UtcNow
        };
        _db.Payments.Add(payment);
        try
        {
            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new BusinessException(BusinessErrorType.Conflict,
                "PAYMENT_DUPLICATE", "Khoản thanh toán đã được khai trước đó.", ex);
        }
        return payment;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Payment>> ListTenantAsync(Guid invoiceId,
        string tenantUserId, CancellationToken ct)
    {
        if (!await InvoiceAccessQueries.TenantInvoices(_db, tenantUserId)
                .AnyAsync(x => x.Id == invoiceId, ct))
            throw NotFound();
        return await _db.Payments.AsNoTracking()
            .Where(x => x.InvoiceId == invoiceId && x.SubmittedByUserId == tenantUserId)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .ToListAsync(ct);
    }

    private static BusinessException NotFound() => new(BusinessErrorType.NotFound,
        "INVOICE_NOT_FOUND", "Không tìm thấy hóa đơn.");
}
