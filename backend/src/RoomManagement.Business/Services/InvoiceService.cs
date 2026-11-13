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

    public async Task<MonthlyBillingWorklist> MonthlyWorklistAsync(Guid propertyId,
        short? year, byte? month, CancellationToken ct)
    {
        var previous = VietnamToday().AddDays(1 - VietnamToday().Day).AddDays(-1);
        var y = year ?? (short)previous.Year;
        var m = month ?? (byte)previous.Month;
        if (y is < 2000 or > 2100 || m is < 1 or > 12 || new DateOnly(y, m, 1) > previous)
            throw Validation("INVOICE_PERIOD_INVALID", "Kỳ chốt hóa đơn không hợp lệ.");
        var owner = await OwnerAsync(ct);
        if (!await _inputs.PropertyOwnedAsync(propertyId, owner, ct))
            throw NotFound("PROPERTY_NOT_FOUND", "Không tìm thấy khu trọ.");
        var candidates = await _invoices.ListMonthlyCandidatesAsync(propertyId, owner, y, m, ct);
        var first = new DateOnly(y, m, 1);
        var electricRate = await _inputs.RateAtAsync(propertyId, UtilityType.Electricity, first, ct);
        var waterRate = await _inputs.RateAtAsync(propertyId, UtilityType.Water, first, ct);
        var items = new List<MonthlyBillingWorkItem>(candidates.Count);
        foreach (var candidate in candidates)
        {
            var missing = new List<string>();
            if (candidate.InvoiceId is null)
            {
                if (electricRate is null) missing.Add("ElectricityRate");
                if (waterRate is null) missing.Add("WaterRate");
                var contract = await _invoices.GetOwnedContractAsync(propertyId, candidate.ContractId, owner, ct);
                if (contract is not null)
                {
                    if (await _inputs.ReadingForContractAsync(contract, UtilityType.Electricity, y, m, ct) is null)
                        missing.Add("ElectricityReading");
                    if (await _inputs.ReadingForContractAsync(contract, UtilityType.Water, y, m, ct) is null)
                        missing.Add("WaterReading");
                }
            }
            var needsAttention = false;
            if (candidate.InvoiceId is null && missing.Count == 0)
            {
                try { await PreviewMonthlyAsync(propertyId, new MonthlyInvoiceInput(candidate.ContractId, y, m), ct); }
                catch (BusinessException ex) { missing.Add(ex.Code); needsAttention = true; }
            }
            items.Add(new MonthlyBillingWorkItem(candidate.ContractId, candidate.RoomId,
                candidate.RoomNumber, candidate.ContractCode,
                candidate.InvoiceStatus == InvoiceStatus.Issued ? "Issued" :
                candidate.InvoiceStatus == InvoiceStatus.Draft ? "Draft" :
                needsAttention ? "NeedsAttention" :
                missing.Count > 0 ? "MissingInputs" : "ReadyToPreview",
                missing, candidate.InvoiceId, candidate.InvoiceStatus));
        }
        return new MonthlyBillingWorklist(y, m, items);
    }

    public async Task<InvoicePreviewResult> PreviewMonthlyAsync(Guid propertyId, MonthlyInvoiceInput input, CancellationToken ct)
    {
        var owner = await OwnerAsync(ct);
        if (input.Year is < 2000 or > 2100 || input.Month is < 1 or > 12 ||
            input.DiscountAmount < 0 || input.DiscountAmount != decimal.Round(input.DiscountAmount, 2) ||
            input.DiscountReason is { Length: > 255 } ||
            input.DiscountAmount > 0 && string.IsNullOrWhiteSpace(input.DiscountReason))
            throw Validation("INVOICE_PERIOD_INVALID", "Kỳ hóa đơn không hợp lệ.");
        var contract = await _invoices.GetOwnedContractAsync(propertyId, input.ContractId, owner, ct)
            ?? throw NotFound("CONTRACT_NOT_FOUND", "Không tìm thấy hợp đồng.");
        var periodStart = new DateOnly(input.Year, input.Month, 1);
        var periodEnd = new DateOnly(input.Year, input.Month, DateTime.DaysInMonth(input.Year, input.Month));
        var currentMonth = new DateOnly(VietnamToday().Year, VietnamToday().Month, 1);
        if (contract.Status is not (ContractStatus.Active or ContractStatus.PendingMoveOut or ContractStatus.PendingSettlement) ||
            contract.Status == ContractStatus.PendingMoveOut && periodStart >= currentMonth ||
            contract.Status == ContractStatus.PendingSettlement &&
            (contract.HandoverOn is null || periodStart > new DateOnly(contract.HandoverOn.Value.Year, contract.HandoverOn.Value.Month, 1)))
            throw Invalid("CONTRACT_NOT_BILLABLE", "Hợp đồng hoặc kỳ này chưa sẵn sàng lập hóa đơn.");
        var electricity = await _inputs.ReadingForContractAsync(contract, UtilityType.Electricity, input.Year, input.Month, ct);
        var water = await _inputs.ReadingForContractAsync(contract, UtilityType.Water, input.Year, input.Month, ct);
        var electricityRate = await _inputs.RateAtAsync(propertyId, UtilityType.Electricity, periodStart, ct);
        var waterRate = await _inputs.RateAtAsync(propertyId, UtilityType.Water, periodStart, ct);
        if (electricity is null || water is null || electricityRate is null || waterRate is null)
        {
            var missingInputs = new List<string>();
            if (electricity is null) missingInputs.Add("chỉ số điện");
            if (water is null) missingInputs.Add("chỉ số nước");
            if (electricityRate is null) missingInputs.Add("đơn giá điện có hiệu lực từ đầu kỳ");
            if (waterRate is null) missingInputs.Add("đơn giá nước có hiệu lực từ đầu kỳ");
            throw Validation("INVOICE_INPUT_INCOMPLETE",
                $"Kỳ {input.Month:D2}/{input.Year} còn thiếu: {string.Join(", ", missingInputs)}.");
        }

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
        var result = _calculator.Calculate(contract, input.Year, input.Month, issueDate,
            electricity, electricityRate, water, waterRate, services);
        if (input.DiscountAmount > result.TotalAmount)
            throw Validation("INVOICE_DISCOUNT_EXCEEDS_TOTAL", "Giảm trừ vượt tổng tiền hóa đơn.");
        if (input.DiscountAmount > 0)
            result = result with {
                Items = result.Items.Concat([new CalculatedInvoiceItem(InvoiceItemType.Discount,
                    input.DiscountReason!.Trim(), 1, input.DiscountAmount, -input.DiscountAmount, null)]).ToList(),
                TotalAmount = result.TotalAmount - input.DiscountAmount
            };
        return result with {
            ElectricityReading = new MeterCostSnapshot(electricity.PreviousValue, electricity.CurrentValue,
                electricity.CurrentValue - electricity.PreviousValue, electricityRate.UnitPrice,
                (electricity.CurrentValue - electricity.PreviousValue) * electricityRate.UnitPrice),
            WaterReading = new MeterCostSnapshot(water.PreviousValue, water.CurrentValue,
                water.CurrentValue - water.PreviousValue, waterRate.UnitPrice,
                (water.CurrentValue - water.PreviousValue) * waterRate.UnitPrice)
        };
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

    public async Task<Invoice> CreateOneOffAsync(Guid propertyId, OneOffInvoiceInput input, CancellationToken ct)
    {
        var owner = await OwnerAsync(ct);
        if (input.Type is not (InvoiceType.Damage or InvoiceType.Other) ||
            input.Note is { Length: > 1000 } ||
            input.Items is null || input.Items.Count is < 1 or > 50 ||
            input.Items.Any(x => x.Type is not (InvoiceItemType.Damage or InvoiceItemType.Other)) ||
            (input.Source is not null && input.Source != "EarlyExitSettlement") ||
            (input.Source is null && input.IdempotencyKey is not null) ||
            (input.Source is not null && (string.IsNullOrWhiteSpace(input.IdempotencyKey) ||
                input.IdempotencyKey.Trim().Length > 100)))
            throw Validation("INVOICE_ONE_OFF_INVALID", "Thông tin hóa đơn riêng không hợp lệ.");
        var editable = ValidateEditable(input.Items, false);
        var contract = await _invoices.GetOwnedContractAsync(propertyId, input.ContractId, owner, ct)
            ?? throw NotFound("CONTRACT_NOT_FOUND", "Không tìm thấy hợp đồng.");
        var operationKey = input.Source is null ? null : input.IdempotencyKey!.Trim();
        var requestHash = operationKey is null ? null : SettlementChargeIdentity.RequestHash(
            input.ContractId, input.Type, input.DueDate, input.Note, editable);
        var fingerprint = operationKey is null ? null : SettlementChargeIdentity.ChargeFingerprint(
            input.ContractId, input.Type, editable);
        if (operationKey is not null)
        {
            var existing = await _invoices.GetOwnedBySettlementKeyAsync(input.ContractId, operationKey, owner, ct);
            if (SettlementChargeIdentity.VerifyRetry(existing, requestHash!) is { } retry) return retry;
            var sameCharge = await _invoices.GetOwnedBySettlementFingerprintAsync(input.ContractId, fingerprint!, owner, ct);
            if (sameCharge is not null) return sameCharge;
        }
        if (input.DueDate < VietnamToday())
            throw Validation("INVOICE_ONE_OFF_INVALID", "Hạn thanh toán hóa đơn riêng không được ở quá khứ.");
        if (contract.Status is not (ContractStatus.Active or ContractStatus.PendingMoveOut or ContractStatus.PendingSettlement))
            throw Invalid("CONTRACT_NOT_BILLABLE", "Hợp đồng không thể lập hóa đơn riêng.");
        if (operationKey is not null && contract.Status != ContractStatus.PendingSettlement)
            throw new BusinessException(BusinessErrorType.Conflict, "SETTLEMENT_CHARGES_CLOSED",
                "Chỉ thêm phí bàn giao khi hợp đồng đang chờ tất toán.");
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var items = editable.Select(x => NewItem(id, x, now)).ToList();
        var total = items.Sum(x => x.Amount);
        if (total <= 0 || total > 9_999_999_999_999_999.99m)
            throw Validation("INVOICE_ONE_OFF_INVALID", "Tổng hóa đơn riêng không hợp lệ.");
        var invoice = new Invoice
        {
            Id = id, ContractId = contract.Id, RoomId = contract.RoomId,
            InvoiceCode = $"ONE-{id:N}", InvoiceType = input.Type,
            IssueDate = VietnamToday(), DueDate = input.DueDate,
            Note = input.Note?.Trim(), TotalAmount = total,
            CreationSource = input.Source, SettlementOperationKey = operationKey,
            SettlementRequestHash = requestHash, SettlementChargeFingerprint = fingerprint,
            Status = InvoiceStatus.Draft, CreatedAt = now
        };
        try { return await _invoices.CreateDraftAsync(invoice, items, owner, ct); }
        catch (BusinessException ex) when (operationKey is not null && ex.Code == "INVOICE_ALREADY_EXISTS")
        {
            var existing = await _invoices.GetOwnedBySettlementKeyAsync(input.ContractId, operationKey, owner, ct);
            if (SettlementChargeIdentity.VerifyRetry(existing, requestHash!) is { } retry) return retry;
            var sameCharge = await _invoices.GetOwnedBySettlementFingerprintAsync(input.ContractId, fingerprint!, owner, ct);
            if (sameCharge is not null) return sameCharge;
            throw;
        }
    }

    public async Task<Invoice> ReplaceDraftItemsAsync(Guid invoiceId, DraftInvoiceItemsInput input, CancellationToken ct)
    {
        if (input.Items is null || input.Items.Count > 100)
            throw Validation("INVOICE_ITEMS_INVALID", "Danh sách khoản tiền không hợp lệ.");
        return await _invoices.ReplaceDraftItemsAsync(invoiceId, await OwnerAsync(ct),
            ValidateEditable(input.Items, true), ct);
    }

    public async Task<InvoiceAdjustment> AddAdjustmentAsync(Guid invoiceId, InvoiceAdjustmentInput input, CancellationToken ct)
    {
        if (!Enum.IsDefined(input.Type) || input.Amount <= 0 ||
            input.Amount > 9_999_999_999_999_999.99m ||
            input.Amount != decimal.Round(input.Amount, 2) ||
            string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length > 500 ||
            string.IsNullOrWhiteSpace(input.IdempotencyKey) || input.IdempotencyKey.Length > 100)
            throw Validation("INVOICE_ADJUSTMENT_INVALID", "Khoản cộng/trừ không hợp lệ.");
        return await _invoices.AddAdjustmentAsync(invoiceId, await OwnerAsync(ct),
            input with { Reason = input.Reason.Trim(), IdempotencyKey = input.IdempotencyKey.Trim() }, ct);
    }

    public async Task<IReadOnlyList<InvoiceAdjustment>> ListAdjustmentsAsync(Guid invoiceId, CancellationToken ct) =>
        await _invoices.ListAdjustmentsAsync(invoiceId, await OwnerAsync(ct), ct);

    public async Task<InvoiceAttention> AttentionAsync(Guid propertyId, CancellationToken ct)
    {
        var owner = await OwnerAsync(ct);
        if (!await _inputs.PropertyOwnedAsync(propertyId, owner, ct))
            throw NotFound("PROPERTY_NOT_FOUND", "Không tìm thấy khu trọ.");
        return await _invoices.AttentionAsync(propertyId, owner, ct);
    }

    private static IReadOnlyList<EditableInvoiceItem> ValidateEditable(
        IReadOnlyList<EditableInvoiceItem> items, bool allowServicesAndDiscounts)
    {
        foreach (var item in items)
        {
            if (item.Type is not (InvoiceItemType.Service or InvoiceItemType.Damage or InvoiceItemType.Other or InvoiceItemType.Discount) ||
                !allowServicesAndDiscounts && item.Type is not (InvoiceItemType.Damage or InvoiceItemType.Other) ||
                string.IsNullOrWhiteSpace(item.Description) || item.Description.Trim().Length > 255 ||
                item.Quantity <= 0 || item.Quantity > 9_999_999_999_999_999.99m ||
                item.Quantity != decimal.Round(item.Quantity, 2) ||
                item.UnitPrice < 0 || item.UnitPrice > 9_999_999_999_999_999.99m ||
                item.UnitPrice != decimal.Round(item.UnitPrice, 2) ||
                item.UnitPrice > 0 && item.Quantity > 9_999_999_999_999_999.99m / item.UnitPrice)
                throw Validation("INVOICE_ITEMS_INVALID", "Khoản tiền không hợp lệ.");
        }
        return items.Select(x => x with { Description = x.Description.Trim() }).ToList();
    }

    private static InvoiceItem NewItem(Guid invoiceId, EditableInvoiceItem x, DateTime now)
    {
        var amount = decimal.Round(x.Quantity * x.UnitPrice, 2, MidpointRounding.AwayFromZero);
        return new InvoiceItem
        {
            Id = Guid.NewGuid(), InvoiceId = invoiceId, ItemType = x.Type,
            Description = x.Description, Quantity = x.Quantity, UnitPrice = x.UnitPrice,
            Amount = x.Type == InvoiceItemType.Discount ? -amount : amount,
            CreatedAt = now
        };
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
