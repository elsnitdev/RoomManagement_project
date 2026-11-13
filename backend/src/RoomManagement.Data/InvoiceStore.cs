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

    public async Task<IReadOnlyList<MonthlyBillingCandidate>> ListMonthlyCandidatesAsync(
        Guid propertyId, string ownerId, short year, byte month, CancellationToken ct)
    {
        var first = new DateOnly(year, month, 1);
        var last = first.AddMonths(1).AddDays(-1);
        var contracts = await (from contract in _db.Contracts.AsNoTracking()
            join room in _db.Rooms.AsNoTracking() on contract.RoomId equals room.Id
            join property in _db.Properties.AsNoTracking() on room.PropertyId equals property.Id
            where property.Id == propertyId && property.OwnerUserId == ownerId && property.IsActive &&
                contract.StartDate <= last &&
                (contract.EndDate == null || contract.EndDate >= first) &&
                (contract.ClosedOn == null || contract.ClosedOn >= first) &&
                (contract.HandoverOn == null || contract.HandoverOn >= first) &&
                (contract.Status == ContractStatus.Active || contract.Status == ContractStatus.PendingMoveOut ||
                 contract.Status == ContractStatus.PendingSettlement || contract.Status == ContractStatus.Expired ||
                 contract.Status == ContractStatus.Terminated)
            orderby room.RoomNumber, contract.StartDate
            select new { contract.Id, contract.RoomId, room.RoomNumber, contract.ContractCode })
            .ToListAsync(ct);
        var ids = contracts.Select(x => x.Id).ToArray();
        var invoices = await _db.Invoices.AsNoTracking().Where(x => ids.Contains(x.ContractId) &&
            x.InvoiceType == InvoiceType.Monthly && x.BillingYear == year && x.BillingMonth == month &&
            x.Status != InvoiceStatus.Cancelled).Select(x => new { x.ContractId, x.Id, x.Status }).ToListAsync(ct);
        return contracts.Select(x => {
            var invoice = invoices.FirstOrDefault(i => i.ContractId == x.Id);
            return new MonthlyBillingCandidate(x.Id, x.RoomId, x.RoomNumber, x.ContractCode,
                invoice?.Id, invoice?.Status);
        }).ToList();
    }

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

    public Task<Invoice?> GetOwnedBySettlementKeyAsync(Guid contractId, string key, string ownerId, CancellationToken ct) =>
        OwnedInvoices(ownerId).AsNoTracking().SingleOrDefaultAsync(x =>
            x.ContractId == contractId && x.SettlementOperationKey == key, ct);

    public Task<Invoice?> GetOwnedBySettlementFingerprintAsync(Guid contractId, string fingerprint, string ownerId, CancellationToken ct) =>
        OwnedInvoices(ownerId).AsNoTracking().SingleOrDefaultAsync(x => x.ContractId == contractId &&
            x.CreationSource == "EarlyExitSettlement" && x.SettlementChargeFingerprint == fingerprint &&
            x.Status != InvoiceStatus.Cancelled, ct);

    public async Task<IReadOnlyList<InvoiceSummaryResult>> ListForContractAsync(Guid contractId, CancellationToken ct)
    {
        var rows = await (from invoice in _db.Invoices.AsNoTracking()
            join balance in _db.InvoiceBalances.AsNoTracking() on invoice.Id equals balance.InvoiceId
            where invoice.ContractId == contractId && invoice.Status != InvoiceStatus.Cancelled
            orderby invoice.IssueDate descending, invoice.Id descending
            select new { Invoice = invoice, Balance = balance }).ToListAsync(ct);
        return rows.Select(x => new InvoiceSummaryResult(x.Invoice, Balance(x.Balance))).ToList();
    }

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
        var adjustments = await _db.InvoiceAdjustments.AsNoTracking().Where(x => x.InvoiceId == invoice.Id)
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(ct);
        var detail = new InvoiceDetailResult(invoice, items, Balance(balance), tenants)
        { Adjustments = adjustments };
        if (invoice.InvoiceType != InvoiceType.Monthly || invoice.BillingYear is null || invoice.BillingMonth is null)
            return detail;

        var readings = await _db.MeterReadings.AsNoTracking().Where(x =>
            x.RoomId == invoice.RoomId && (x.ContractId == invoice.ContractId || x.ContractId == null) &&
            x.Year == invoice.BillingYear && x.Month == invoice.BillingMonth).ToListAsync(ct);
        var utilityItems = items.Where(x => x.ItemType is InvoiceItemType.Electricity or InvoiceItemType.Water).ToList();
        var propertyId = await _db.Rooms.AsNoTracking().Where(x => x.Id == invoice.RoomId)
            .Select(x => x.PropertyId).SingleAsync(ct);
        var periodStart = new DateOnly(invoice.BillingYear.Value, invoice.BillingMonth.Value, 1);

        async Task<MeterCostSnapshot?> ReadingAsync(UtilityType type, InvoiceItemType itemType)
        {
            var line = utilityItems.SingleOrDefault(x => x.ItemType == itemType);
            var reading = readings.FirstOrDefault(x => x.Id == line?.ReferenceId && x.MeterType == type)
                ?? readings.FirstOrDefault(x => x.ContractId == invoice.ContractId && x.MeterType == type)
                ?? readings.FirstOrDefault(x => x.ContractId == null && x.MeterType == type);
            if (reading is null) return null;
            // A zero-usage utility has no invoice item. Rates for invoiced periods cannot be replaced.
            var unitPrice = line?.UnitPrice ?? await _db.UtilityRates.AsNoTracking()
                .Where(x => x.PropertyId == propertyId && x.UtilityType == type &&
                    x.EffectiveFrom <= periodStart && (x.EffectiveTo == null || x.EffectiveTo >= periodStart))
                .Select(x => (decimal?)x.UnitPrice).SingleOrDefaultAsync(ct);
            if (unitPrice is null) return null;
            return new MeterCostSnapshot(reading.PreviousValue, reading.CurrentValue,
                reading.CurrentValue - reading.PreviousValue, unitPrice.Value, line?.Amount ?? 0);
        }

        return detail with {
            ElectricityReading = await ReadingAsync(UtilityType.Electricity, InvoiceItemType.Electricity),
            WaterReading = await ReadingAsync(UtilityType.Water, InvoiceItemType.Water)
        };
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
                                     ? contract.Status == ContractStatus.Active || contract.Status == ContractStatus.PendingMoveOut ||
                                       contract.Status == ContractStatus.PendingSettlement
                                     : contract.Status == ContractStatus.Draft || contract.Status == ContractStatus.Active ||
                                       contract.Status == ContractStatus.PendingMoveOut || contract.Status == ContractStatus.PendingSettlement)
                           select contract.Id).AnyAsync(ct);
        if (!owned) throw NotFound();
        if (invoice.CreationSource == "EarlyExitSettlement")
        {
            var existingOperation = await _db.Invoices.AsNoTracking().SingleOrDefaultAsync(x =>
                x.ContractId == invoice.ContractId && x.SettlementOperationKey == invoice.SettlementOperationKey, ct);
            if (existingOperation is not null)
            {
                if (existingOperation.SettlementRequestHash == invoice.SettlementRequestHash)
                    return existingOperation;
                throw new BusinessException(BusinessErrorType.Conflict, "SETTLEMENT_OPERATION_KEY_CONFLICT",
                    "Mã thao tác đã được dùng cho hóa đơn có nội dung khác.");
            }
            var sameCharge = await _db.Invoices.AsNoTracking().SingleOrDefaultAsync(x =>
                x.ContractId == invoice.ContractId && x.CreationSource == "EarlyExitSettlement" &&
                x.SettlementChargeFingerprint == invoice.SettlementChargeFingerprint &&
                x.Status != InvoiceStatus.Cancelled, ct);
            if (sameCharge is not null) return sameCharge;
            var handover = await _db.Contracts.AsNoTracking().Where(x => x.Id == invoice.ContractId)
                .Select(x => new { x.Status, x.HandoverOn }).SingleAsync(ct);
            if (handover.Status != ContractStatus.PendingSettlement || handover.HandoverOn is null ||
                await _db.Invoices.AnyAsync(x => x.ContractId == invoice.ContractId &&
                    x.InvoiceType == InvoiceType.Monthly && x.BillingYear == handover.HandoverOn.Value.Year &&
                    x.BillingMonth == handover.HandoverOn.Value.Month && x.Status == InvoiceStatus.Issued, ct))
                throw new BusinessException(BusinessErrorType.Conflict, "SETTLEMENT_CHARGES_CLOSED",
                    "Hóa đơn cuối đã phát hành. Hãy thêm phí mới bằng thao tác điều chỉnh riêng.");
        }
        if (invoice.InvoiceType == InvoiceType.Deposit &&
            await _db.Invoices.AnyAsync(x => x.ContractId == invoice.ContractId &&
                x.InvoiceType == InvoiceType.Deposit, ct))
            throw new BusinessException(BusinessErrorType.Conflict,
                "DEPOSIT_INVOICE_EXISTS", "Hợp đồng đã có lịch sử hóa đơn cọc.");
        if (invoice.InvoiceType == InvoiceType.Monthly &&
            await _db.Invoices.AnyAsync(x => x.ContractId == invoice.ContractId &&
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
            await _db.Invoices.AnyAsync(x => x.Id != invoice.Id && x.ContractId == invoice.ContractId &&
                x.InvoiceType == InvoiceType.Monthly && x.BillingYear == invoice.BillingYear &&
                x.BillingMonth == invoice.BillingMonth && x.Status != InvoiceStatus.Cancelled, ct))
            throw new BusinessException(BusinessErrorType.Conflict, "ROOM_MONTH_INVOICE_EXISTS",
                "Phòng có hóa đơn tháng khác chưa hủy cho kỳ này.");
        var contract = await _db.Contracts.AsNoTracking().Where(x => x.Id == invoice.ContractId)
            .Select(x => new { x.Status, x.EndDate }).SingleAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        if (contract.EndDate is { } end && end < today &&
            contract.Status is not (ContractStatus.PendingMoveOut or ContractStatus.PendingSettlement) ||
            (invoice.InvoiceType == InvoiceType.Monthly
                ? contract.Status is not (ContractStatus.Active or ContractStatus.PendingMoveOut or ContractStatus.PendingSettlement)
                : contract.Status is not (ContractStatus.Draft or ContractStatus.Active or
                    ContractStatus.PendingMoveOut or ContractStatus.PendingSettlement)))
            throw Invalid("CONTRACT_CLOSED", "Hợp đồng đã đóng, không thể phát hành hóa đơn.");
        var amounts = await _db.InvoiceItems.AsNoTracking().Where(x => x.InvoiceId == invoiceId)
            .Select(x => x.Amount).ToListAsync(ct);
        if (amounts.Count == 0 || amounts.Sum() != invoice.TotalAmount)
            throw Invalid("INVOICE_TOTAL_MISMATCH", "Tổng hóa đơn không khớp các dòng tiền.");
        if (invoice.DueDate < today)
        {
            if (contract.Status is not (ContractStatus.PendingMoveOut or ContractStatus.PendingSettlement))
                throw Invalid("INVOICE_DUE_DATE_PASSED", "Hạn thanh toán đã qua.");
            invoice.DueDate = today;
        }
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

    public async Task<InvoiceAttention> AttentionAsync(Guid propertyId, string ownerId, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var invoiceQuery = from invoice in OwnedInvoices(ownerId).AsNoTracking()
                           join room in _db.Rooms.AsNoTracking() on invoice.RoomId equals room.Id
                           where room.PropertyId == propertyId && invoice.Status == InvoiceStatus.Issued
                           select invoice;
        var rows = await (from invoice in invoiceQuery
                          join balance in _db.InvoiceBalances.AsNoTracking() on invoice.Id equals balance.InvoiceId
                          where balance.RemainingAmount > 0
                          orderby invoice.DueDate < today ? 0 : balance.TotalPaid + balance.TotalDepositApplied > 0 ? 1 : 2,
                              invoice.DueDate, invoice.Id
                          select new { Invoice = invoice, Balance = balance }).ToListAsync(ct);
        var pending = await (from invoice in invoiceQuery
                             join payment in _db.Payments.AsNoTracking() on invoice.Id equals payment.InvoiceId
                             join user in _db.Users.AsNoTracking() on payment.SubmittedByUserId equals user.Id into users
                             from user in users.DefaultIfEmpty()
                             where payment.Status == PaymentStatus.Pending
                             orderby payment.CreatedAt, payment.Id
                             select new PendingInvoicePayment(invoice.Id, invoice.InvoiceCode, invoice.RoomId,
                                 payment.Id, payment.ClaimedAmount, user == null ? null : user.FullName,
                                 payment.PaidAt)).ToListAsync(ct);
        return new InvoiceAttention(rows.Select(x => new InvoiceSummaryResult(x.Invoice, Balance(x.Balance))).ToList(), pending);
    }

    public async Task<Invoice> ReplaceDraftItemsAsync(Guid invoiceId, string ownerId,
        IReadOnlyList<EditableInvoiceItem> replacements, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await InvoiceTransactionLock.AcquireAsync(_db, invoiceId, ct);
        var invoice = await OwnedInvoices(ownerId).SingleOrDefaultAsync(x => x.Id == invoiceId, ct)
            ?? throw NotFound();
        if (invoice.Status != InvoiceStatus.Draft)
            throw Invalid("INVOICE_INVALID_STATUS", "Chỉ có thể sửa hóa đơn nháp.");
        var original = await _db.InvoiceItems.Where(x => x.InvoiceId == invoiceId).ToListAsync(ct);
        var editable = original.Where(x => x.ItemType is InvoiceItemType.Service or InvoiceItemType.Damage or
            InvoiceItemType.Other or InvoiceItemType.Discount).ToDictionary(x => x.Id);
        var submittedIds = replacements.Where(x => x.Id.HasValue).Select(x => x.Id!.Value).ToList();
        if (submittedIds.Count != submittedIds.Distinct().Count() ||
            submittedIds.Any(id => !editable.ContainsKey(id)))
            throw Validation("INVOICE_ITEMS_INVALID", "Dòng hóa đơn không hợp lệ.");
        foreach (var existing in editable.Values.Where(x => !submittedIds.Contains(x.Id)))
            _db.InvoiceItems.Remove(existing);
        var now = DateTime.UtcNow;
        foreach (var item in replacements)
        {
            if (item.Type is not (InvoiceItemType.Service or InvoiceItemType.Damage or InvoiceItemType.Other or InvoiceItemType.Discount) ||
                string.IsNullOrWhiteSpace(item.Description) || item.Description.Length > 255 ||
                item.Quantity <= 0 || item.UnitPrice < 0)
                throw Validation("INVOICE_ITEMS_INVALID", "Dòng hóa đơn không hợp lệ.");
            if (item.Id.HasValue && editable[item.Id.Value].ItemType == InvoiceItemType.Service &&
                item.Type != InvoiceItemType.Service)
                throw Validation("INVOICE_ITEMS_INVALID", "Không thể đổi loại dịch vụ gốc.");
            var amount = decimal.Round(item.Quantity * item.UnitPrice, 2, MidpointRounding.AwayFromZero);
            var entity = item.Id.HasValue ? editable[item.Id.Value] : new InvoiceItem
            {
                Id = Guid.NewGuid(), InvoiceId = invoiceId, CreatedAt = now
            };
            entity.ItemType = item.Type;
            if (item.Type != InvoiceItemType.Service) entity.ReferenceId = null;
            entity.Description = item.Description.Trim();
            entity.Quantity = item.Quantity;
            entity.UnitPrice = item.UnitPrice;
            entity.Amount = item.Type == InvoiceItemType.Discount ? -amount : amount;
            if (!item.Id.HasValue) _db.InvoiceItems.Add(entity);
        }
        var fixedTotal = original.Where(x => !editable.ContainsKey(x.Id)).Sum(x => x.Amount);
        var total = fixedTotal + replacements.Sum(x => x.Type == InvoiceItemType.Discount
            ? -decimal.Round(x.Quantity * x.UnitPrice, 2, MidpointRounding.AwayFromZero)
            : decimal.Round(x.Quantity * x.UnitPrice, 2, MidpointRounding.AwayFromZero));
        if (total <= 0 || total > 9_999_999_999_999_999.99m ||
            original.Count - editable.Count + replacements.Count == 0)
            throw Validation("INVOICE_TOTAL_INVALID", "Tổng hóa đơn không hợp lệ.");
        invoice.TotalAmount = total;
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return invoice;
    }

    public async Task<IReadOnlyList<InvoiceAdjustment>> ListAdjustmentsAsync(Guid invoiceId,
        string ownerId, CancellationToken ct)
    {
        if (!await OwnedInvoices(ownerId).AnyAsync(x => x.Id == invoiceId, ct)) throw NotFound();
        return await _db.InvoiceAdjustments.AsNoTracking().Where(x => x.InvoiceId == invoiceId)
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(ct);
    }

    public async Task<InvoiceAdjustment> AddAdjustmentAsync(Guid invoiceId, string ownerId,
        InvoiceAdjustmentInput input, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await InvoiceTransactionLock.AcquireAsync(_db, invoiceId, ct);
        var invoice = await OwnedInvoices(ownerId).AsNoTracking().SingleOrDefaultAsync(x => x.Id == invoiceId, ct)
            ?? throw NotFound();
        if (invoice.Status != InvoiceStatus.Issued ||
            invoice.InvoiceType is not (InvoiceType.Monthly or InvoiceType.Damage or InvoiceType.Other))
            throw Invalid("INVOICE_INVALID_STATUS", "Hóa đơn này không thể điều chỉnh.");
        var existing = await _db.InvoiceAdjustments.AsNoTracking()
            .SingleOrDefaultAsync(x => x.IdempotencyKey == input.IdempotencyKey, ct);
        if (existing is not null)
        {
            if (existing.InvoiceId != invoiceId || existing.AdjustmentType != input.Type ||
                existing.Amount != input.Amount || existing.Reason != input.Reason)
                throw new BusinessException(BusinessErrorType.Conflict, "ADJUSTMENT_KEY_CONFLICT", "Mã thao tác đã được sử dụng.");
            return existing;
        }
        var balance = await _db.InvoiceBalances.AsNoTracking().SingleAsync(x => x.InvoiceId == invoiceId, ct);
        if (input.Type != InvoiceAdjustmentType.AdditionalCharge && balance.EffectiveAmount < input.Amount)
            throw Validation("ADJUSTMENT_EXCEEDS_TOTAL", "Khoản giảm vượt số phải thu.");
        if (input.Type == InvoiceAdjustmentType.AdditionalCharge &&
            balance.EffectiveAmount > 9_999_999_999_999_999.99m - input.Amount)
            throw Validation("ADJUSTMENT_EXCEEDS_TOTAL", "Tổng phải thu vượt giới hạn.");
        var adjustment = new InvoiceAdjustment
        {
            Id = Guid.NewGuid(), InvoiceId = invoiceId, AdjustmentType = input.Type,
            Amount = input.Amount, Reason = input.Reason, IdempotencyKey = input.IdempotencyKey,
            CreatedByUserId = ownerId, CreatedAt = DateTime.UtcNow
        };
        _db.InvoiceAdjustments.Add(adjustment);
        try { await _db.SaveChangesAsync(ct); await tx.CommitAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new BusinessException(BusinessErrorType.Conflict,
                "ADJUSTMENT_KEY_CONFLICT", "Mã thao tác đã được sử dụng.", ex);
        }
        return adjustment;
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
