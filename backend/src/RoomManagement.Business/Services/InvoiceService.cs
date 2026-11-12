using RoomManagement.Business.DTOs;
using RoomManagement.Business.Errors;
using RoomManagement.Business.Interfaces;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Business.Services;

public sealed class InvoiceService
{
    private readonly ICurrentLandlord _current;
    private readonly ILandlordStore _landlords;
    private readonly IInvoiceStore _invoices;
    private readonly IBillingInputStore _inputs;
    private readonly MonthlyInvoiceCalculator _calculator;

    public InvoiceService(ICurrentLandlord current, ILandlordStore landlords,
        IInvoiceStore invoices, IBillingInputStore inputs, MonthlyInvoiceCalculator calculator)
    {
        _current = current;
        _landlords = landlords;
        _invoices = invoices;
        _inputs = inputs;
        _calculator = calculator;
    }

    private async Task<string> OwnerAsync(CancellationToken ct)
    {
        var owner = _current.GetUserId();
        if (!await _landlords.IsActiveLandlordAsync(owner, ct))
            throw Invalid("LANDLORD_UNAVAILABLE", "Chủ trọ không khả dụng.");
        return owner;
    }

    public async Task<InvoicePreviewResult> PreviewMonthlyAsync(Guid propertyId, MonthlyInvoiceInput input, CancellationToken ct)
    {
        var owner = await OwnerAsync(ct);
        if (input.Year is < 2000 or > 2100 || input.Month is < 1 or > 12)
            throw Validation("INVOICE_PERIOD_INVALID", "Kỳ hóa đơn không hợp lệ.");
        var contract = await _invoices.GetOwnedContractAsync(propertyId, input.ContractId, owner, ct)
            ?? throw NotFound("CONTRACT_NOT_FOUND", "Không tìm thấy hợp đồng.");
        if (contract.Status != ContractStatus.Active)
            throw Invalid("CONTRACT_NOT_ACTIVE", "Chỉ hợp đồng đang hoạt động mới có hóa đơn tháng.");
        var periodStart = new DateOnly(input.Year, input.Month, 1);
        var periodEnd = new DateOnly(input.Year, input.Month, DateTime.DaysInMonth(input.Year, input.Month));
        var electricity = await _inputs.ReadingAtAsync(contract.RoomId, UtilityType.Electricity, input.Year, input.Month, ct);
        var water = await _inputs.ReadingAtAsync(contract.RoomId, UtilityType.Water, input.Year, input.Month, ct);
        var electricityRate = await _inputs.RateAtAsync(propertyId, UtilityType.Electricity, periodStart, ct);
        var waterRate = await _inputs.RateAtAsync(propertyId, UtilityType.Water, periodStart, ct);
        if (electricity is null || water is null || electricityRate is null || waterRate is null)
            throw Validation("INVOICE_INPUT_INCOMPLETE", "Thiếu chỉ số hoặc đơn giá điện nước của kỳ.");

        var assignments = await _inputs.ListRoomServicesAsync(contract.RoomId, ct);
        var services = new List<BillableRoomService>();
        foreach (var assignment in assignments)
        {
            var item = assignment.Assignment;
            if (item.StartDate > periodEnd || item.EndDate is { } end && end < periodStart) continue;
            if (item.StartDate > periodStart || item.EndDate is { } partialEnd && partialEnd < periodEnd)
                throw Validation("INVOICE_SERVICE_PARTIAL_PERIOD",
                    "Dịch vụ áp dụng một phần kỳ chưa có quy tắc phân bổ tiền.");
            var price = item.CustomPrice ?? assignment.Service.DefaultPrice
                ?? throw Validation("SERVICE_PRICE_MISSING", "Dịch vụ chưa có đơn giá.");
            if (assignment.Service.PropertyId != propertyId)
                throw Validation("SERVICE_PROPERTY_MISMATCH", "Dịch vụ không thuộc khu trọ của phòng.");
            services.Add(new BillableRoomService(item.ServiceId, assignment.Service.Name, price, item.Quantity));
        }
        var issueDate = VietnamToday();
        return _calculator.Calculate(contract, input.Year, input.Month, issueDate,
            electricity, electricityRate, water, waterRate, services);
    }

    public async Task<Invoice> CreateMonthlyAsync(Guid propertyId, MonthlyInvoiceInput input, CancellationToken ct)
    {
        var preview = await PreviewMonthlyAsync(propertyId, input, ct); // Recompute on the server.
        var owner = await OwnerAsync(ct);
        var invoiceId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var invoice = new Invoice
        {
            Id = invoiceId, ContractId = preview.ContractId, RoomId = preview.RoomId,
            InvoiceCode = $"INV-{preview.Year:D4}{preview.Month:D2}-{invoiceId:N}",
            InvoiceType = InvoiceType.Monthly, BillingYear = preview.Year, BillingMonth = preview.Month,
            IssueDate = preview.IssueDate, DueDate = preview.DueDate,
            TotalAmount = preview.TotalAmount, Status = InvoiceStatus.Draft, CreatedAt = now
        };
        var items = preview.Items.Select(x => new InvoiceItem
        {
            Id = Guid.NewGuid(), InvoiceId = invoiceId, ItemType = x.Type,
            Description = x.Description, Quantity = x.Quantity, UnitPrice = x.UnitPrice,
            Amount = x.Amount, ReferenceId = x.ReferenceId, CreatedAt = now
        }).ToList();
        return await _invoices.CreateDraftAsync(invoice, items, owner, ct);
    }

    public async Task<Invoice> CreateDepositAsync(Guid contractId, CancellationToken ct)
    {
        var owner = await OwnerAsync(ct);
        var contract = await _invoices.GetOwnedContractByIdAsync(contractId, owner, ct)
            ?? throw NotFound("CONTRACT_NOT_FOUND", "Không tìm thấy hợp đồng.");
        if (contract.Status is not (ContractStatus.Draft or ContractStatus.Active) || contract.DepositAmount <= 0)
            throw Invalid("DEPOSIT_NOT_AVAILABLE", "Hợp đồng không có khoản cọc cần thu.");
        var today = VietnamToday();
        var due = contract.StartDate > today ? contract.StartDate : today;
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var invoice = new Invoice
        {
            Id = id, ContractId = contractId, RoomId = contract.RoomId,
            InvoiceCode = $"DEP-{id:N}", InvoiceType = InvoiceType.Deposit,
            IssueDate = today, DueDate = due, TotalAmount = contract.DepositAmount,
            Status = InvoiceStatus.Draft, CreatedAt = now
        };
        var item = new InvoiceItem
        {
            Id = Guid.NewGuid(), InvoiceId = id, ItemType = InvoiceItemType.Deposit,
            Description = "Tiền đặt cọc", Quantity = 1, UnitPrice = contract.DepositAmount,
            Amount = contract.DepositAmount, CreatedAt = now
        };
        return await _invoices.CreateDraftAsync(invoice, [item], owner, ct);
    }

    public async Task<Invoice?> FindDepositAsync(Guid contractId, CancellationToken ct)
    {
        var owner = await OwnerAsync(ct);
        _ = await _invoices.GetOwnedContractByIdAsync(contractId, owner, ct)
            ?? throw NotFound("CONTRACT_NOT_FOUND", "Không tìm thấy hợp đồng.");
        return await _invoices.GetOwnedDepositAsync(contractId, owner, ct);
    }

    public async Task<PageResult<InvoiceSummaryResult>> ListAsync(Guid propertyId, InvoiceFilter filter, CancellationToken ct)
    {
        var owner = await OwnerAsync(ct);
        if (filter.Page is < 1 or > 1000000 || filter.PageSize is < 1 or > 100 ||
            filter.Year is < 2000 or > 2100 || filter.Month is < 1 or > 12 ||
            filter.Status is { } status && !Enum.IsDefined(status))
            throw Validation("INVOICE_FILTER_INVALID", "Bộ lọc hóa đơn không hợp lệ.");
        if (!await _inputs.PropertyOwnedAsync(propertyId, owner, ct))
            throw NotFound("PROPERTY_NOT_FOUND", "Không tìm thấy khu trọ.");
        var (items, total) = await _invoices.ListOwnedAsync(propertyId, owner, filter, ct);
        return new PageResult<InvoiceSummaryResult>(items, total, filter.Page, filter.PageSize);
    }

    public async Task<InvoiceDetailResult> GetDetailAsync(Guid invoiceId, CancellationToken ct)
    {
        var owner = await OwnerAsync(ct);
        return await _invoices.GetOwnedDetailAsync(invoiceId, owner, ct)
            ?? throw NotFound("INVOICE_NOT_FOUND", "Không tìm thấy hóa đơn.");
    }

    public async Task<PageResult<InvoiceSummaryResult>> ListMineAsync(
        string tenantUserId, InvoiceFilter filter, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tenantUserId) || filter.Page is < 1 or > 1000000 ||
            filter.PageSize is < 1 or > 100 || filter.Year is < 2000 or > 2100 ||
            filter.Month is < 1 or > 12 || filter.Status is not null && filter.Status != InvoiceStatus.Issued)
            throw Validation("INVOICE_FILTER_INVALID", "Bộ lọc hóa đơn không hợp lệ.");
        var (items, total) = await _invoices.ListTenantAsync(tenantUserId, filter, ct);
        return new PageResult<InvoiceSummaryResult>(items, total, filter.Page, filter.PageSize);
    }

    public async Task<InvoiceDetailResult> GetMineAsync(string tenantUserId, Guid invoiceId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tenantUserId))
            throw NotFound("INVOICE_NOT_FOUND", "Không tìm thấy hóa đơn.");
        return await _invoices.GetTenantDetailAsync(invoiceId, tenantUserId, ct)
            ?? throw NotFound("INVOICE_NOT_FOUND", "Không tìm thấy hóa đơn.");
    }

    public async Task<Property> GetMyPaymentPropertyAsync(string tenantUserId, Guid invoiceId, CancellationToken ct) =>
        !string.IsNullOrWhiteSpace(tenantUserId)
            ? await _invoices.GetTenantPaymentPropertyAsync(invoiceId, tenantUserId, ct)
                ?? throw NotFound("INVOICE_NOT_FOUND", "Không tìm thấy hóa đơn.")
            : throw NotFound("INVOICE_NOT_FOUND", "Không tìm thấy hóa đơn.");

    public Task<bool> CanDeclareMyPaymentAsync(string tenantUserId, Guid invoiceId, CancellationToken ct) =>
        _invoices.CanTenantDeclarePaymentAsync(invoiceId, tenantUserId,
            VietnamDate.Today, ct);

    public async Task<Invoice> IssueAsync(Guid invoiceId, CancellationToken ct) =>
        await _invoices.IssueAsync(invoiceId, await OwnerAsync(ct), ct);

    public async Task<Invoice> CancelAsync(Guid invoiceId, CancellationToken ct) =>
        await _invoices.CancelAsync(invoiceId, await OwnerAsync(ct), ct);

    private static DateOnly VietnamToday() => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
    private static BusinessException Validation(string code, string message) => new(BusinessErrorType.Validation, code, message);
    private static BusinessException Invalid(string code, string message) => new(BusinessErrorType.InvalidOperation, code, message);
    private static BusinessException NotFound(string code, string message) => new(BusinessErrorType.NotFound, code, message);
}
