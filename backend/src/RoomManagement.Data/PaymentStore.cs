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
        if (input.Amount > remaining)
            throw new BusinessException(BusinessErrorType.Validation,
                "PAYMENT_EXCEEDS_AMOUNT_DUE", "Số tiền khai vượt số còn phải thu. Nếu đã chuyển dư, vui lòng liên hệ chủ trọ để đối soát.");

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
    public async Task<IReadOnlyList<TenantPaymentResult>> ListTenantAsync(Guid invoiceId,
        string tenantUserId, CancellationToken ct)
    {
        if (!await InvoiceAccessQueries.TenantInvoices(_db, tenantUserId)
                .AnyAsync(x => x.Id == invoiceId, ct))
            throw NotFound();
        var results = await _db.Payments.AsNoTracking()
            .Where(x => x.InvoiceId == invoiceId && x.SubmittedByUserId == tenantUserId)
            .Select(payment => new TenantPaymentResult(payment,
                _db.PaymentReviewEvents.Where(review => review.PaymentId == payment.Id)
                    .OrderByDescending(review => review.ReviewedAt).ThenByDescending(review => review.Id)
                    .Select(review => review.Reason).FirstOrDefault()))
            .ToListAsync(ct);
        return results.OrderByDescending(x => x.Payment.CreatedAt)
            .ThenByDescending(x => x.Payment.Id).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OwnerPaymentResult>> ListOwnedAsync(Guid invoiceId,
        string ownerId, CancellationToken ct)
    {
        if (!await InvoiceAccessQueries.OwnedInvoices(_db, ownerId)
                .AnyAsync(x => x.Id == invoiceId, ct))
            throw NotFound();
        var results = await OwnerResults(invoiceId).ToListAsync(ct);
        return results.OrderByDescending(x => x.Payment.CreatedAt)
            .ThenByDescending(x => x.Payment.Id).ToList();
    }

    /// <inheritdoc />
    public async Task<OwnerPaymentResult> ReviewAsync(Guid paymentId, string ownerId,
        ReviewPaymentInput input, CancellationToken ct, string? actorUserId = null)
    {
        var actor = actorUserId ?? ownerId;
        var invoiceId = await (from paymentRow in _db.Payments.AsNoTracking()
                               join invoiceRow in InvoiceAccessQueries.OwnedInvoices(_db, ownerId).AsNoTracking()
                                   on paymentRow.InvoiceId equals invoiceRow.Id
                               where paymentRow.Id == paymentId
                               select (Guid?)invoiceRow.Id).SingleOrDefaultAsync(ct) ?? throw PaymentNotFound();

        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await InvoiceTransactionLock.AcquireAsync(_db, invoiceId, ct);
        var invoice = await InvoiceAccessQueries.OwnedInvoices(_db, ownerId)
            .AsNoTracking().SingleOrDefaultAsync(x => x.Id == invoiceId, ct) ?? throw PaymentNotFound();
        if (invoice.Status != InvoiceStatus.Issued)
            throw new BusinessException(BusinessErrorType.InvalidOperation,
                "INVOICE_NOT_ISSUED", "Chỉ hóa đơn đã phát hành mới được ghi nhận thanh toán.");
        var payment = await _db.Payments.SingleOrDefaultAsync(x => x.Id == paymentId && x.InvoiceId == invoiceId, ct)
            ?? throw PaymentNotFound();
        if (payment.Status != PaymentStatus.Pending)
        {
            var lastReview = await _db.PaymentReviewEvents.AsNoTracking()
                .Where(x => x.PaymentId == paymentId)
                .OrderByDescending(x => x.ReviewedAt).ThenByDescending(x => x.Id)
                .FirstOrDefaultAsync(ct);
            if (payment.Status == input.Status && payment.VerifiedAmount == input.VerifiedAmount &&
                lastReview?.Reason == input.Reason)
                return await OwnerResults(invoiceId, paymentId).SingleAsync(ct);
            throw new BusinessException(BusinessErrorType.Conflict,
                "PAYMENT_ALREADY_REVIEWED", "Khoản thanh toán đã được xử lý.");
        }

        var now = DateTime.UtcNow;
        payment.Status = input.Status;
        payment.VerifiedAmount = input.Status == PaymentStatus.Success ? input.VerifiedAmount : null;
        payment.VerifiedAt = input.Status == PaymentStatus.Success ? now : null;
        payment.VerifiedByUserId = input.Status == PaymentStatus.Success ? actor : null;
        if (input.Status == PaymentStatus.Success) payment.PaidAt ??= now;
        _db.PaymentReviewEvents.Add(new PaymentReviewEvent
        {
            Id = Guid.NewGuid(), PaymentId = payment.Id,
            PreviousStatus = PaymentStatus.Pending, NewStatus = input.Status,
            NewVerifiedAmount = payment.VerifiedAmount, Reason = input.Reason,
            ReviewedByUserId = actor, ReviewedAt = now
        });
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await OwnerResults(invoiceId, paymentId).SingleAsync(ct);
    }

    /// <inheritdoc />
    public async Task<OwnerPaymentResult> RecordReceivedAsync(Guid invoiceId, string ownerId,
        RecordReceivedPaymentInput input, CancellationToken ct, string? actorUserId = null)
    {
        var actor = actorUserId ?? ownerId;
        if (!await InvoiceAccessQueries.OwnedInvoices(_db, ownerId)
                .AnyAsync(x => x.Id == invoiceId, ct))
            throw NotFound();
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await InvoiceTransactionLock.AcquireAsync(_db, invoiceId, ct);
        var invoice = await InvoiceAccessQueries.OwnedInvoices(_db, ownerId)
            .AsNoTracking().SingleOrDefaultAsync(x => x.Id == invoiceId, ct) ?? throw NotFound();
        var key = $"landlord:{input.IdempotencyKey:N}";
        var existing = await _db.Payments.AsNoTracking()
            .SingleOrDefaultAsync(x => x.IdempotencyKey == key, ct);
        if (existing is not null)
        {
            if (existing.InvoiceId != invoiceId || existing.SubmittedByUserId != actor ||
                existing.ClaimedAmount != input.Amount || existing.PaymentMethod != input.Method ||
                input.PaidAt is { } paidAt && existing.PaidAt != paidAt.UtcDateTime ||
                existing.TransactionCode != input.TransactionCode || existing.Note != input.Note ||
                existing.Status != PaymentStatus.Success)
                throw new BusinessException(BusinessErrorType.Conflict,
                    "PAYMENT_IDEMPOTENCY_CONFLICT", "Khóa gửi lại đã được dùng cho khoản khác.");
            return await OwnerResults(invoiceId, existing.Id).SingleAsync(ct);
        }
        if (invoice.Status != InvoiceStatus.Issued)
            throw new BusinessException(BusinessErrorType.InvalidOperation,
                "INVOICE_NOT_ISSUED", "Chỉ hóa đơn đã phát hành mới được ghi nhận thanh toán.");
        var remaining = await _db.InvoiceBalances.AsNoTracking()
            .Where(x => x.InvoiceId == invoiceId)
            .Select(x => x.RemainingAmount).SingleAsync(ct);
        if (remaining <= 0)
            throw new BusinessException(BusinessErrorType.Conflict,
                "INVOICE_NO_AMOUNT_DUE", "Hóa đơn đã thu đủ hoặc đang dư tiền; vui lòng tải lại hóa đơn.");

        var now = DateTime.UtcNow;
        var payment = new Payment
        {
            Id = Guid.NewGuid(), InvoiceId = invoiceId, SubmittedByUserId = actor,
            ClaimedAmount = input.Amount, VerifiedAmount = input.Amount,
            PaymentMethod = input.Method, Status = PaymentStatus.Success,
            PaidAt = input.PaidAt?.UtcDateTime ?? now, VerifiedAt = now, VerifiedByUserId = actor,
            TransactionCode = input.TransactionCode, IdempotencyKey = key,
            Note = input.Note, CreatedAt = now
        };
        _db.Payments.Add(payment);
        _db.PaymentReviewEvents.Add(new PaymentReviewEvent
        {
            Id = Guid.NewGuid(), PaymentId = payment.Id,
            PreviousStatus = PaymentStatus.Pending, NewStatus = PaymentStatus.Success,
            NewVerifiedAmount = input.Amount,
            Reason = "Chủ trọ ghi nhận đã nhận tiền trực tiếp.",
            ReviewedByUserId = actor, ReviewedAt = now
        });
        try
        {
            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new BusinessException(BusinessErrorType.Conflict,
                "PAYMENT_DUPLICATE", "Khoản thanh toán đã được ghi nhận trước đó.", ex);
        }
        return await OwnerResults(invoiceId, payment.Id).SingleAsync(ct);
    }

    public async Task<IReadOnlyList<Refund>> ListOverpaymentRefundsAsync(Guid invoiceId, string ownerId, CancellationToken ct)
    {
        if (!await InvoiceAccessQueries.OwnedInvoices(_db, ownerId).AnyAsync(x => x.Id == invoiceId, ct)) throw NotFound();
        return await _db.Refunds.AsNoTracking().Where(x => x.InvoiceId == invoiceId && x.IsOverpaymentRefund)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).ToListAsync(ct);
    }

    public async Task<Refund> RecordOverpaymentRefundAsync(Guid invoiceId, string ownerId,
        decimal amount, string? transactionCode, Guid idempotencyKey, CancellationToken ct, string? actorUserId = null)
    {
        var actor = actorUserId ?? ownerId;
        if (!await InvoiceAccessQueries.OwnedInvoices(_db, ownerId).AnyAsync(x => x.Id == invoiceId, ct))
            throw NotFound();
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await InvoiceTransactionLock.AcquireAsync(_db, invoiceId, ct);
        var invoice = await InvoiceAccessQueries.OwnedInvoices(_db, ownerId).AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == invoiceId, ct) ?? throw NotFound();
        var key = $"overpayment:{idempotencyKey:N}";
        var existing = await _db.Refunds.AsNoTracking().SingleOrDefaultAsync(x => x.IdempotencyKey == key, ct);
        if (existing is not null)
        {
            if (existing.InvoiceId != invoiceId || existing.CreatedByUserId != actor ||
                existing.Amount != amount || existing.TransactionCode != transactionCode || !existing.IsOverpaymentRefund)
                throw new BusinessException(BusinessErrorType.Conflict, "REFUND_IDEMPOTENCY_CONFLICT", "Khóa xác nhận đã được dùng cho khoản hoàn khác.");
            return existing;
        }
        if (invoice.Status != InvoiceStatus.Issued)
            throw new BusinessException(BusinessErrorType.InvalidOperation, "INVOICE_NOT_ISSUED", "Chỉ có thể hoàn tiền cho hóa đơn đã phát hành.");
        var remaining = await _db.InvoiceBalances.AsNoTracking().Where(x => x.InvoiceId == invoiceId)
            .Select(x => x.RemainingAmount).SingleAsync(ct);
        if (remaining >= 0 || amount > -remaining)
            throw new BusinessException(BusinessErrorType.Conflict, "REFUND_EXCEEDS_CREDIT", "Số tiền hoàn vượt tiền dư hiện tại; vui lòng tải lại hóa đơn.");
        var now = DateTime.UtcNow;
        var refund = new Refund { Id = Guid.NewGuid(), InvoiceId = invoiceId, Amount = amount,
            IsOverpaymentRefund = true, Status = RefundStatus.Success, RefundedAt = now,
            Reason = "Chủ trọ xác nhận đã hoàn tiền thu vượt ngoài hệ thống.", TransactionCode = transactionCode,
            IdempotencyKey = key, CreatedByUserId = actor, CreatedAt = now };
        _db.Refunds.Add(refund);
        try { await _db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        { throw new BusinessException(BusinessErrorType.Conflict, "REFUND_DUPLICATE", "Mã hoàn tiền đã tồn tại.", ex); }
        return refund;
    }

    private IQueryable<OwnerPaymentResult> OwnerResults(Guid invoiceId, Guid? paymentId = null) =>
        _db.Payments.AsNoTracking()
            .Where(payment => payment.InvoiceId == invoiceId &&
                (paymentId == null || payment.Id == paymentId))
            .Select(payment => new OwnerPaymentResult(payment,
                _db.Users.Where(user => user.Id == payment.SubmittedByUserId)
                    .Select(user => user.FullName).FirstOrDefault(),
                _db.PaymentReviewEvents.Where(review => review.PaymentId == payment.Id)
                    .OrderByDescending(review => review.ReviewedAt).ThenByDescending(review => review.Id)
                    .Select(review => review.Reason).FirstOrDefault()));

    private static BusinessException NotFound() => new(BusinessErrorType.NotFound,
        "INVOICE_NOT_FOUND", "Không tìm thấy hóa đơn.");

    private static BusinessException PaymentNotFound() => new(BusinessErrorType.NotFound,
        "PAYMENT_NOT_FOUND", "Không tìm thấy khoản thanh toán.");
}
