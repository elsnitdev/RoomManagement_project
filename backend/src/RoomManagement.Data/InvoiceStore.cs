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

    /// <summary>Tạo truy vấn hóa đơn thuộc các khu trọ của chủ trọ.</summary>
    private IQueryable<Invoice> OwnedInvoices(string ownerId) =>
        InvoiceAccessQueries.OwnedInvoices(_db, ownerId);

    /// <summary>Tạo truy vấn hóa đơn đã phát hành mà người thuê được xem.</summary>
    private IQueryable<Invoice> TenantInvoices(string tenantUserId) =>
        InvoiceAccessQueries.TenantInvoices(_db, tenantUserId);

    public Task<Property?> GetTenantPaymentPropertyAsync(Guid invoiceId, string tenantUserId, CancellationToken ct) =>
        (from invoice in TenantInvoices(tenantUserId).AsNoTracking()
         join room in _db.Rooms.AsNoTracking() on invoice.RoomId equals room.Id
         join property in _db.Properties.AsNoTracking() on room.PropertyId equals property.Id
         where invoice.Id == invoiceId
         select property).SingleOrDefaultAsync(ct);

    public Task<bool> CanTenantDeclarePaymentAsync(Guid invoiceId, string tenantUserId, DateOnly today, CancellationToken ct) =>
        TenantInvoices(tenantUserId).AnyAsync(invoice => invoice.Id == invoiceId &&
            _db.ContractTenants.Any(link => link.ContractId == invoice.ContractId &&
                (link.LeaveDate == null || link.LeaveDate >= today) &&
                _db.TenantProfiles.Any(profile => profile.Id == link.TenantId && profile.UserId == tenantUserId)), ct);

    /// <inheritdoc />
    public Task<Contract?> GetOwnedContractAsync(Guid propertyId, Guid contractId, string ownerId, CancellationToken ct) =>
        (from contract in _db.Contracts.AsNoTracking()
         join room in _db.Rooms.AsNoTracking() on contract.RoomId equals room.Id
         join property in _db.Properties.AsNoTracking() on room.PropertyId equals property.Id
         where contract.Id == contractId && property.Id == propertyId &&
               property.OwnerUserId == ownerId && property.IsActive
         select contract).SingleOrDefaultAsync(ct);

    /// <inheritdoc />
    public Task<Contract?> GetOwnedContractByIdAsync(Guid contractId, string ownerId, CancellationToken ct) =>
        (from contract in _db.Contracts.AsNoTracking()
         join room in _db.Rooms.AsNoTracking() on contract.RoomId equals room.Id
         join property in _db.Properties.AsNoTracking() on room.PropertyId equals property.Id
         where contract.Id == contractId && property.OwnerUserId == ownerId && property.IsActive
         select contract).SingleOrDefaultAsync(ct);

    public Task<Invoice?> GetOwnedDepositAsync(Guid contractId, string ownerId, CancellationToken ct) =>
        OwnedInvoices(ownerId).AsNoTracking()
            .Where(x => x.ContractId == contractId && x.InvoiceType == InvoiceType.Deposit)
            .OrderBy(x => x.Status == InvoiceStatus.Cancelled ? 1 : 0)
            .ThenByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    public async Task<(IReadOnlyList<InvoiceSummaryResult> Items, long Total)> ListOwnedAsync(
        Guid propertyId, string ownerId, InvoiceFilter filter, CancellationToken ct)
    {
        var query = from invoice in OwnedInvoices(ownerId).AsNoTracking()
                    join room in _db.Rooms.AsNoTracking() on invoice.RoomId equals room.Id
                    where room.PropertyId == propertyId
                    select invoice;
        return await ListQueryAsync(query, filter, ct);
    }

    /// <inheritdoc />
    public Task<(IReadOnlyList<InvoiceSummaryResult> Items, long Total)> ListTenantAsync(
        string tenantUserId, InvoiceFilter filter, CancellationToken ct) =>
        ListQueryAsync(TenantInvoices(tenantUserId).AsNoTracking(), filter, ct);

    /// <summary>Lọc, phân trang và tính số dư cho danh sách hóa đơn.</summary>
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

    /// <inheritdoc />
    public async Task<InvoiceDetailResult?> GetOwnedDetailAsync(Guid invoiceId, string ownerId, CancellationToken ct)
    {
        var invoice = await OwnedInvoices(ownerId).AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == invoiceId, ct);
        return await DetailAsync(invoice, null, ct);
    }

    /// <inheritdoc />
    public async Task<InvoiceDetailResult?> GetTenantDetailAsync(Guid invoiceId, string tenantUserId, CancellationToken ct)
    {
        var invoice = await TenantInvoices(tenantUserId).AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == invoiceId, ct);
        return await DetailAsync(invoice, tenantUserId, ct);
    }

    /// <summary>Ghép hóa đơn với các dòng tiền và số dư.</summary>
    private async Task<InvoiceDetailResult?> DetailAsync(Invoice? invoice, string? tenantUserId, CancellationToken ct)
    {
        if (invoice is null) return null;
        var items = await _db.InvoiceItems.AsNoTracking().Where(x => x.InvoiceId == invoice.Id)
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(ct);
        var balance = await _db.InvoiceBalances.AsNoTracking()
            .SingleAsync(x => x.InvoiceId == invoice.Id, ct);
        var tenants = await (from link in _db.ContractTenants.AsNoTracking()
                             join profile in _db.TenantProfiles.AsNoTracking() on link.TenantId equals profile.Id
                             where link.ContractId == invoice.ContractId &&
                                 (tenantUserId == null || profile.UserId == tenantUserId)
                             orderby link.JoinDate, link.Id
                             select new InvoiceTenantResult(profile.FullName, profile.PhoneNumber, profile.Email,
                                 link.IsContactPerson, link.JoinDate, link.LeaveDate)).ToListAsync(ct);
        return new InvoiceDetailResult(invoice, items, Balance(balance), tenants);
    }

    /// <inheritdoc />
    public async Task<Invoice> CreateDraftAsync(Invoice invoice, IReadOnlyList<InvoiceItem> items,
        string ownerId, CancellationToken ct)
    {
        if (invoice.Status != InvoiceStatus.Draft || items.Count == 0 ||
            invoice.DueDate < invoice.IssueDate || invoice.TotalAmount != items.Sum(x => x.Amount) ||
            items.Any(x => x.InvoiceId != invoice.Id || x.Quantity <= 0 || x.UnitPrice < 0))
            throw Validation("INVOICE_TOTAL_MISMATCH", "Hóa đơn và các dòng tiền không hợp lệ.");
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await _db.Rooms.FromSqlInterpolated($"SELECT * FROM dbo.Rooms WITH (UPDLOCK, HOLDLOCK) WHERE Id = {invoice.RoomId}")
            .AsNoTracking().SingleAsync(ct);
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
        if (invoice.InvoiceType == InvoiceType.Deposit &&
            await _db.Invoices.AnyAsync(x => x.ContractId == invoice.ContractId &&
                x.InvoiceType == InvoiceType.Deposit, ct))
            throw new BusinessException(BusinessErrorType.Conflict,
                "DEPOSIT_INVOICE_EXISTS", "Hợp đồng đã có lịch sử hóa đơn cọc.");
        if (invoice.InvoiceType == InvoiceType.Monthly &&
            await _db.Invoices.AnyAsync(x => x.RoomId == invoice.RoomId &&
                x.InvoiceType == InvoiceType.Monthly && x.BillingYear == invoice.BillingYear &&
                x.BillingMonth == invoice.BillingMonth && x.Status != InvoiceStatus.Cancelled, ct))
            throw new BusinessException(BusinessErrorType.Conflict, "ROOM_MONTH_INVOICE_EXISTS",
                "Phòng đã có hóa đơn tháng chưa hủy cho kỳ này.");
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

    /// <inheritdoc />
    public async Task<Invoice> IssueAsync(Guid invoiceId, string ownerId, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var roomId = await OwnedInvoices(ownerId).AsNoTracking()
            .Where(x => x.Id == invoiceId).Select(x => (Guid?)x.RoomId).SingleOrDefaultAsync(ct)
            ?? throw NotFound();
        await _db.Rooms.FromSqlInterpolated($"SELECT * FROM dbo.Rooms WITH (UPDLOCK, HOLDLOCK) WHERE Id = {roomId}")
            .AsNoTracking().SingleAsync(ct);
        await InvoiceTransactionLock.AcquireAsync(_db, invoiceId, ct);
        var invoice = await OwnedInvoices(ownerId).SingleOrDefaultAsync(x => x.Id == invoiceId, ct)
            ?? throw NotFound();
        if (invoice.Status != InvoiceStatus.Draft)
            throw Invalid("INVOICE_INVALID_STATUS", "Chỉ hóa đơn nháp mới được phát hành.");
        if (invoice.InvoiceType == InvoiceType.Monthly &&
            await _db.Invoices.AnyAsync(x => x.Id != invoice.Id && x.RoomId == invoice.RoomId &&
                x.InvoiceType == InvoiceType.Monthly && x.BillingYear == invoice.BillingYear &&
                x.BillingMonth == invoice.BillingMonth && x.Status != InvoiceStatus.Cancelled, ct))
            throw new BusinessException(BusinessErrorType.Conflict, "ROOM_MONTH_INVOICE_EXISTS",
                "Phòng có hóa đơn tháng khác chưa hủy cho kỳ này.");
        var contract = await _db.Contracts.AsNoTracking().Where(x => x.Id == invoice.ContractId)
            .Select(x => new { x.Status, x.EndDate }).SingleAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        if (contract.EndDate is { } end && end < today ||
            (invoice.InvoiceType == InvoiceType.Monthly
                ? contract.Status != ContractStatus.Active
                : contract.Status is not (ContractStatus.Draft or ContractStatus.Active)))
            throw Invalid("CONTRACT_CLOSED", "Hợp đồng đã đóng, không thể phát hành hóa đơn.");
        var amounts = await _db.InvoiceItems.AsNoTracking().Where(x => x.InvoiceId == invoiceId)
            .Select(x => x.Amount).ToListAsync(ct);
        if (amounts.Count == 0 || amounts.Sum() != invoice.TotalAmount)
            throw Invalid("INVOICE_TOTAL_MISMATCH", "Tổng hóa đơn không khớp các dòng tiền.");
        if (invoice.DueDate < today)
            throw Invalid("INVOICE_DUE_DATE_PASSED", "Hạn thanh toán đã qua.");
        invoice.IssueDate = today;
        invoice.Status = InvoiceStatus.Issued;
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return invoice;
    }

    /// <inheritdoc />
    public async Task<Invoice> CancelAsync(Guid invoiceId, string ownerId, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await InvoiceTransactionLock.AcquireAsync(_db, invoiceId, ct);
        var invoice = await OwnedInvoices(ownerId).SingleOrDefaultAsync(x => x.Id == invoiceId, ct)
            ?? throw NotFound();
        if (invoice.Status == InvoiceStatus.Cancelled)
            throw Invalid("INVOICE_INVALID_STATUS", "Hóa đơn đã hủy.");
        var hasMoneyHistory = await InvoiceFinancialActivity.ExistsAsync(_db, invoice, ct);
        if (hasMoneyHistory)
            throw Invalid("INVOICE_HAS_FINANCIAL_ACTIVITY", "Hóa đơn đã có giao dịch tài chính.");
        invoice.Status = InvoiceStatus.Cancelled;
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return invoice;
    }

    /// <summary>Chuyển dữ liệu số dư thành kết quả nghiệp vụ.</summary>
    private static InvoiceBalanceResult Balance(InvoiceBalanceRow x) =>
        new(x.EffectiveAmount, x.TotalPaid, x.TotalRefunded, x.TotalDepositApplied, x.RemainingAmount);
    /// <summary>Tạo lỗi không tìm thấy hóa đơn.</summary>
    private static BusinessException NotFound() =>
        new(BusinessErrorType.NotFound, "INVOICE_NOT_FOUND", "Không tìm thấy hóa đơn.");
    /// <summary>Tạo lỗi dữ liệu hóa đơn không hợp lệ.</summary>
    private static BusinessException Validation(string code, string message) =>
        new(BusinessErrorType.Validation, code, message);
    /// <summary>Tạo lỗi thao tác hóa đơn không hợp lệ.</summary>
    private static BusinessException Invalid(string code, string message) =>
        new(BusinessErrorType.InvalidOperation, code, message);
}
