using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RoomManagement.Business.DTOs;
using RoomManagement.Business.Errors;
using RoomManagement.Business.Interfaces;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Data;

public sealed class InvoiceStore : IInvoiceStore
{
    private readonly RoomManagementDbContext _db;
    public InvoiceStore(RoomManagementDbContext db) => _db = db;

    private IQueryable<Invoice> OwnedInvoices(string ownerId) =>
        from invoice in _db.Invoices
        join room in _db.Rooms on invoice.RoomId equals room.Id
        join property in _db.Properties on room.PropertyId equals property.Id
        where property.OwnerUserId == ownerId
        select invoice;

    private IQueryable<Invoice> TenantInvoices(string tenantUserId) =>
        _db.Invoices.Where(invoice => invoice.Status == InvoiceStatus.Issued &&
            _db.ContractTenants.Any(link => link.ContractId == invoice.ContractId &&
                (invoice.InvoiceType == InvoiceType.Monthly
                    ? invoice.BillingYear != null && invoice.BillingMonth != null &&
                      (link.JoinDate.Year < invoice.BillingYear ||
                       link.JoinDate.Year == invoice.BillingYear && link.JoinDate.Month <= invoice.BillingMonth) &&
                      (link.LeaveDate == null || link.LeaveDate.Value.Year > invoice.BillingYear ||
                       link.LeaveDate.Value.Year == invoice.BillingYear && link.LeaveDate.Value.Month >= invoice.BillingMonth)
                    : link.JoinDate <= invoice.IssueDate &&
                      (link.LeaveDate == null || link.LeaveDate >= invoice.IssueDate)) &&
                _db.TenantProfiles.Any(profile => profile.Id == link.TenantId &&
                    profile.UserId == tenantUserId)));

    public Task<Contract?> GetOwnedContractAsync(Guid propertyId, Guid contractId, string ownerId, CancellationToken ct) =>
        (from contract in _db.Contracts.AsNoTracking()
         join room in _db.Rooms.AsNoTracking() on contract.RoomId equals room.Id
         join property in _db.Properties.AsNoTracking() on room.PropertyId equals property.Id
         where contract.Id == contractId && property.Id == propertyId &&
               property.OwnerUserId == ownerId && property.IsActive
         select contract).SingleOrDefaultAsync(ct);

    public Task<Contract?> GetOwnedContractByIdAsync(Guid contractId, string ownerId, CancellationToken ct) =>
        (from contract in _db.Contracts.AsNoTracking()
         join room in _db.Rooms.AsNoTracking() on contract.RoomId equals room.Id
         join property in _db.Properties.AsNoTracking() on room.PropertyId equals property.Id
         where contract.Id == contractId && property.OwnerUserId == ownerId && property.IsActive
         select contract).SingleOrDefaultAsync(ct);

    public async Task<(IReadOnlyList<InvoiceSummaryResult> Items, long Total)> ListOwnedAsync(
        Guid propertyId, string ownerId, InvoiceFilter filter, CancellationToken ct)
    {
        var query = from invoice in OwnedInvoices(ownerId).AsNoTracking()
                    join room in _db.Rooms.AsNoTracking() on invoice.RoomId equals room.Id
                    where room.PropertyId == propertyId
                    select invoice;
        return await ListQueryAsync(query, filter, ct);
    }

    public Task<(IReadOnlyList<InvoiceSummaryResult> Items, long Total)> ListTenantAsync(
        string tenantUserId, InvoiceFilter filter, CancellationToken ct) =>
        ListQueryAsync(TenantInvoices(tenantUserId).AsNoTracking(), filter, ct);

    private async Task<(IReadOnlyList<InvoiceSummaryResult> Items, long Total)> ListQueryAsync(
        IQueryable<Invoice> query, InvoiceFilter filter, CancellationToken ct)
    {
        if (filter.RoomId.HasValue) query = query.Where(x => x.RoomId == filter.RoomId.Value);
        if (filter.Year.HasValue) query = query.Where(x => x.BillingYear == filter.Year.Value);
        if (filter.Month.HasValue) query = query.Where(x => x.BillingMonth == filter.Month.Value);
        if (filter.Status.HasValue) query = query.Where(x => x.Status == filter.Status.Value);
        var total = await query.LongCountAsync(ct);
        var rows = await (from invoice in query
                          join balance in _db.InvoiceBalances.AsNoTracking() on invoice.Id equals balance.InvoiceId
                          orderby invoice.IssueDate descending, invoice.Id descending
                          select new { Invoice = invoice, Balance = balance })
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync(ct);
        return (rows.Select(x => new InvoiceSummaryResult(x.Invoice, Balance(x.Balance))).ToList(), total);
    }

    public async Task<InvoiceDetailResult?> GetOwnedDetailAsync(Guid invoiceId, string ownerId, CancellationToken ct)
    {
        var invoice = await OwnedInvoices(ownerId).AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == invoiceId, ct);
        return await DetailAsync(invoice, ct);
    }

    public async Task<InvoiceDetailResult?> GetTenantDetailAsync(Guid invoiceId, string tenantUserId, CancellationToken ct)
    {
        var invoice = await TenantInvoices(tenantUserId).AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == invoiceId, ct);
        return await DetailAsync(invoice, ct);
    }

    private async Task<InvoiceDetailResult?> DetailAsync(Invoice? invoice, CancellationToken ct)
    {
        if (invoice is null) return null;
        var items = await _db.InvoiceItems.AsNoTracking().Where(x => x.InvoiceId == invoice.Id)
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(ct);
        var balance = await _db.InvoiceBalances.AsNoTracking()
            .SingleAsync(x => x.InvoiceId == invoice.Id, ct);
        return new InvoiceDetailResult(invoice, items, Balance(balance));
    }

    public async Task<Invoice> CreateDraftAsync(Invoice invoice, IReadOnlyList<InvoiceItem> items,
        string ownerId, CancellationToken ct)
    {
        if (invoice.Status != InvoiceStatus.Draft || items.Count == 0 ||
            invoice.DueDate < invoice.IssueDate || invoice.TotalAmount != items.Sum(x => x.Amount) ||
            items.Any(x => x.InvoiceId != invoice.Id || x.Quantity <= 0 || x.UnitPrice < 0))
            throw Validation("INVOICE_TOTAL_MISMATCH", "Hóa đơn và các dòng tiền không hợp lệ.");
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var owned = await (from contract in _db.Contracts
                           join room in _db.Rooms on contract.RoomId equals room.Id
                           join property in _db.Properties on room.PropertyId equals property.Id
                           where contract.Id == invoice.ContractId && room.Id == invoice.RoomId &&
                                 property.OwnerUserId == ownerId && property.IsActive &&
                                 (invoice.InvoiceType == InvoiceType.Monthly
                                     ? contract.Status == ContractStatus.Active
                                     : contract.Status == ContractStatus.Draft || contract.Status == ContractStatus.Active)
                           select contract.Id).AnyAsync(ct);
        if (!owned) throw NotFound();
        _db.Invoices.Add(invoice);
        _db.InvoiceItems.AddRange(items);
        try
        {
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new BusinessException(BusinessErrorType.Conflict,
                "INVOICE_ALREADY_EXISTS", "Hóa đơn kỳ này hoặc mã hóa đơn đã tồn tại.", ex);
        }
        return invoice;
    }

    public async Task<Invoice> IssueAsync(Guid invoiceId, string ownerId, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await InvoiceTransactionLock.AcquireAsync(_db, invoiceId, ct);
        var invoice = await OwnedInvoices(ownerId).SingleOrDefaultAsync(x => x.Id == invoiceId, ct)
            ?? throw NotFound();
        if (invoice.Status != InvoiceStatus.Draft)
            throw Invalid("INVOICE_INVALID_STATUS", "Chỉ hóa đơn nháp mới được phát hành.");
        var amounts = await _db.InvoiceItems.AsNoTracking().Where(x => x.InvoiceId == invoiceId)
            .Select(x => x.Amount).ToListAsync(ct);
        if (amounts.Count == 0 || amounts.Sum() != invoice.TotalAmount)
            throw Invalid("INVOICE_TOTAL_MISMATCH", "Tổng hóa đơn không khớp các dòng tiền.");
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        if (invoice.DueDate < today)
            throw Invalid("INVOICE_DUE_DATE_PASSED", "Hạn thanh toán đã qua.");
        invoice.IssueDate = today;
        invoice.Status = InvoiceStatus.Issued;
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return invoice;
    }

    public async Task<Invoice> CancelAsync(Guid invoiceId, string ownerId, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await InvoiceTransactionLock.AcquireAsync(_db, invoiceId, ct);
        var invoice = await OwnedInvoices(ownerId).SingleOrDefaultAsync(x => x.Id == invoiceId, ct)
            ?? throw NotFound();
        if (invoice.Status == InvoiceStatus.Cancelled)
            throw Invalid("INVOICE_INVALID_STATUS", "Hóa đơn đã hủy.");
        var hasMoneyHistory = await _db.Payments.AnyAsync(x => x.InvoiceId == invoiceId, ct) ||
            await _db.Refunds.AnyAsync(x => x.InvoiceId == invoiceId, ct) ||
            await _db.InvoiceAdjustments.AnyAsync(x => x.InvoiceId == invoiceId, ct) ||
            await _db.DepositApplications.AnyAsync(x => x.TargetInvoiceId == invoiceId, ct) ||
            invoice.InvoiceType == InvoiceType.Deposit &&
                await _db.DepositApplications.AnyAsync(x => x.ContractId == invoice.ContractId, ct);
        if (hasMoneyHistory)
            throw Invalid("INVOICE_HAS_FINANCIAL_ACTIVITY", "Hóa đơn đã có giao dịch tài chính.");
        invoice.Status = InvoiceStatus.Cancelled;
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return invoice;
    }

    private static InvoiceBalanceResult Balance(InvoiceBalanceRow x) =>
        new(x.EffectiveAmount, x.TotalPaid, x.TotalRefunded, x.TotalDepositApplied, x.RemainingAmount);
    private static BusinessException NotFound() =>
        new(BusinessErrorType.NotFound, "INVOICE_NOT_FOUND", "Không tìm thấy hóa đơn.");
    private static BusinessException Validation(string code, string message) =>
        new(BusinessErrorType.Validation, code, message);
    private static BusinessException Invalid(string code, string message) =>
        new(BusinessErrorType.InvalidOperation, code, message);
}
