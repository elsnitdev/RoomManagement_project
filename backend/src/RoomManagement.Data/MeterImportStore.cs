using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RoomManagement.Business.DTOs;
using RoomManagement.Business.Errors;
using RoomManagement.Business.Interfaces;
using RoomManagement.Business.Services;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Data;

public sealed class MeterImportStore(RoomManagementDbContext db, ILandlordStore landlords,
    ILogger<MeterImportStore> logger) : IMeterImportStore
{
    private const decimal MaxValue = 9_999_999_999_999_999.99m;
    private static readonly ContractStatus[] OccupancyStatuses =
    [
        ContractStatus.Active, ContractStatus.Expired, ContractStatus.Terminated,
        ContractStatus.PendingMoveOut, ContractStatus.PendingSettlement
    ];

    public async Task<MeterImportResult> InspectAsync(Guid propertyId, DateOnly period,
        IReadOnlyList<MeterImportRowInput>? rows, CancellationToken ct, bool includeUnoccupied = false)
    {
        var snapshot = await LoadAsync(propertyId, period, rows?.Select(x => x.RoomId).ToArray(), ct, includeUnoccupied);
        var inputs = rows ?? snapshot.Rooms.Values.OrderBy(x => x.RoomNumber, StringComparer.OrdinalIgnoreCase)
            .SelectMany(room => snapshot.Contracts[room.Id].Where(x => Overlaps(x, period) &&
                    !((x.HandoverOn is not null || x.Status == ContractStatus.Terminated) &&
                      snapshot.Invoices[room.Id].Any(invoice => invoice.ContractId == x.Id &&
                          invoice.Status == InvoiceStatus.Issued)))
                .OrderBy(x => x.StartDate).ThenBy(x => x.Id)
                .Select(contract => new MeterImportRowInput(room.Id, null, null, ContractId: contract.Id))
                .Concat(includeUnoccupied && !snapshot.Contracts[room.Id].Any(x => Overlaps(x, period))
                    ? [new MeterImportRowInput(room.Id, null, null)] : [])).ToList();
        return new MeterImportResult(propertyId, (short)period.Year, (byte)period.Month,
            inputs.Select(row => Assess(snapshot, period, row, committing: false).Row).ToList());
    }

    public async Task<MeterImportRow> SaveAsync(Guid propertyId, DateOnly period, OperatorContext actor,
        MeterImportRowInput input, CancellationToken ct)
    {
        MeterImportRow? assessed = null;
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var room = await db.Rooms.FromSqlInterpolated(
                    $"SELECT * FROM dbo.Rooms WITH (UPDLOCK, HOLDLOCK) WHERE Id = {input.RoomId} AND PropertyId = {propertyId}")
                .AsNoTracking().SingleOrDefaultAsync(ct);
            if (room is null) return Unavailable(input, "", "Không tìm thấy phòng đủ điều kiện trong khu trọ.");
            if (!await landlords.HasPropertyAccessAsync(actor.ActorUserId, propertyId, ct) ||
                !await db.Properties.AnyAsync(p => p.Id == propertyId && p.IsActive && p.OwnerUserId == actor.OwnerLandlordUserId, ct))
                throw BusinessException.NotFound("Không tìm thấy khu trọ.", "PROPERTY_NOT_FOUND");
            // Release the invoice discovery read before taking the applock, as draft editing takes the applock first.
            var draftIds = await db.Invoices.FromSqlRaw("SELECT * FROM dbo.Invoices WITH (READCOMMITTEDLOCK)")
                .AsNoTracking().Where(x =>
                    x.RoomId == room.Id && x.InvoiceType == InvoiceType.Monthly &&
                    x.BillingYear == period.Year && x.BillingMonth == period.Month && x.Status != InvoiceStatus.Cancelled)
                .OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(ct);
            foreach (var id in draftIds)
                await InvoiceTransactionLock.AcquireAsync(db, id, ct);

            var snapshot = await LoadAsync(propertyId, period, [room.Id], ct);
            var (row, contract, invoice) = Assess(snapshot, period, input, committing: true);
            assessed = row;
            if (row.Electricity.Status != "Ready" && row.Water.Status != "Ready")
                return row;

            var electricity = NewReading(row.Electricity, room.Id, contract?.Id, period, UtilityType.Electricity,
                actor, input.ElectricityManual);
            var water = NewReading(row.Water, room.Id, contract?.Id, period, UtilityType.Water,
                actor, input.WaterManual);
            var readings = new[] { electricity, water }.OfType<MeterReading>().ToList();
            if (invoice is not null && readings.Count > 0)
            {
                var result = await UpdateDraftAsync(invoice.Id, propertyId, period, readings, ct);
                row = row with
                {
                    Electricity = ApplyDraftError(row.Electricity, result, UtilityType.Electricity),
                    Water = ApplyDraftError(row.Water, result, UtilityType.Water),
                };
            }
            var electricityOpening = IsOpeningOnly(row.Electricity);
            var waterOpening = IsOpeningOnly(row.Water);
            if (readings.Count == 0 && !electricityOpening && !waterOpening) return row;

            if (electricityOpening || waterOpening)
            {
                db.Rooms.Attach(room);
                if (electricityOpening) room.OpeningElectricity = row.Electricity.PreviousValue;
                if (waterOpening) room.OpeningWater = row.Water.PreviousValue;
            }

            foreach (var reading in readings)
            {
                var cell = reading.MeterType == UtilityType.Electricity ? row.Electricity : row.Water;
                if (cell.ReadingId.HasValue)
                {
                    var stored = snapshot.Readings[room.Id].Single(x => x.Id == reading.Id);
                    db.MeterReadings.Attach(stored).Property(x => x.CurrentValue).CurrentValue = reading.CurrentValue;
                }
                else
                    db.MeterReadings.Add(reading);
            }
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            db.ChangeTracker.Clear();
            foreach (var reading in readings)
                logger.LogInformation("Saved meter reading {ReadingId} for property {PropertyId}, room {RoomId}, meter {MeterType}, period {Period}, actor {ActorId}, source {Source}",
                    reading.Id, propertyId, room.Id, reading.MeterType, period, actor.ActorUserId,
                    (reading.MeterType == UtilityType.Electricity ? input.ElectricityManual : input.WaterManual) ? "Manual" : "Excel");
            return row with
            {
                Electricity = Saved(row.Electricity, electricity, electricityOpening),
                Water = Saved(row.Water, water, waterOpening)
            };
        }
        catch (BusinessException ex) when (ex.Code == "INVOICE_BUSY")
        {
            return Retry(assessed, input);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 1205 or 1222 or 2601 or 2627 })
        {
            db.ChangeTracker.Clear();
            return Retry(assessed, input);
        }
        catch (SqlException ex) when (ex.Number is 1205 or 1222)
        {
            db.ChangeTracker.Clear();
            return Retry(assessed, input);
        }
    }

    private static MeterImportRow Retry(MeterImportRow? row, MeterImportRowInput input)
    {
        const string message = "Dữ liệu đang thay đổi. Kiểm tra lại rồi thử lưu chỉ số.";
        if (row is null)
            return new MeterImportRow(input.RoomId, "", null,
                new("Error", null, input.Electricity, "METER_IMPORT_RETRY", message),
                new("Error", null, input.Water, "METER_IMPORT_RETRY", message), input.ContractId);
        return row with
        {
            Electricity = row.Electricity.Status == "Ready"
                ? Fail(row.Electricity, "METER_IMPORT_RETRY", message) : row.Electricity,
            Water = row.Water.Status == "Ready"
                ? Fail(row.Water, "METER_IMPORT_RETRY", message) : row.Water
        };
    }

    private async Task<Dictionary<UtilityType, (string Code, string Message)>> UpdateDraftAsync(
        Guid invoiceId, Guid propertyId, DateOnly period, List<MeterReading> readings, CancellationToken ct)
    {
        var invoice = await db.Invoices.SingleAsync(x => x.Id == invoiceId, ct);
        var items = await db.InvoiceItems.Where(x => x.InvoiceId == invoiceId).ToListAsync(ct);
        var rates = await db.UtilityRates.AsNoTracking().Where(x => x.PropertyId == propertyId &&
            x.EffectiveFrom <= period && (x.EffectiveTo == null || x.EffectiveTo >= period)).ToListAsync(ct);
        var errors = new Dictionary<UtilityType, (string, string)>();
        var changes = new List<(InvoiceItemType Type, InvoiceItem? Current, MeterReading Reading, decimal Price, decimal Amount)>();
        foreach (var reading in readings)
        {
            var type = reading.MeterType == UtilityType.Electricity ? InvoiceItemType.Electricity : InvoiceItemType.Water;
            var previousItems = items.Where(x => x.ItemType == type).ToList();
            var matchingRates = rates.Where(x => x.UtilityType == reading.MeterType).ToList();
            var used = reading.CurrentValue - reading.PreviousValue;
            if (previousItems.Count > 1 || (used > 0 && matchingRates.Count != 1))
            {
                errors[reading.MeterType] = ("METER_IMPORT_DRAFT_INPUT_INVALID",
                    "Kiểm tra đơn giá hoặc dòng tiền điện nước trong hóa đơn nháp trước khi lưu.");
                continue;
            }
            var price = used == 0 ? 0 : matchingRates[0].UnitPrice;
            decimal amount;
            try { amount = decimal.Round(checked(used * price), 2, MidpointRounding.AwayFromZero); }
            catch (OverflowException) { amount = MaxValue + 1; }
            if (price < 0 || amount > MaxValue)
            {
                errors[reading.MeterType] = ("METER_IMPORT_DRAFT_AMOUNT_INVALID", "Thành tiền điện nước vượt giới hạn.");
                continue;
            }
            changes.Add((type, previousItems.SingleOrDefault(), reading, price, amount));
        }
        var total = items.Sum(x => x.Amount) + changes.Sum(x => x.Amount - (x.Current?.Amount ?? 0));
        if (total is < 0 or > MaxValue || (items.All(x => changes.Any(c => c.Current == x)) &&
            changes.All(x => x.Reading.CurrentValue == x.Reading.PreviousValue)))
        {
            foreach (var change in changes)
                errors[change.Reading.MeterType] = ("METER_IMPORT_DRAFT_TOTAL_INVALID",
                    "Không thể đồng bộ tổng hóa đơn nháp. Kiểm tra các khoản tiền trong hóa đơn.");
            changes.Clear();
        }
        if (changes.Count > 0)
        {
            foreach (var change in changes)
            {
                var quantity = change.Reading.CurrentValue - change.Reading.PreviousValue;
                if (quantity == 0)
                {
                    if (change.Current is not null) db.InvoiceItems.Remove(change.Current);
                    continue;
                }
                var item = change.Current ?? new InvoiceItem
                {
                    Id = Guid.NewGuid(), InvoiceId = invoice.Id, ItemType = change.Type,
                    Description = change.Type == InvoiceItemType.Electricity ? "Điện" : "Nước",
                    CreatedAt = DateTime.UtcNow
                };
                item.Quantity = quantity;
                item.UnitPrice = change.Price;
                item.Amount = change.Amount;
                item.ReferenceId = change.Reading.Id;
                if (change.Current is null) db.InvoiceItems.Add(item);
            }
            invoice.TotalAmount = total;
        }
        else
        {
            // Do not retain invoice/item entities across subsequent room transactions in this batch.
            db.Entry(invoice).State = EntityState.Detached;
            foreach (var item in items) db.Entry(item).State = EntityState.Detached;
        }
        readings.RemoveAll(x => errors.ContainsKey(x.MeterType));
        return errors;
    }

    private async Task<Snapshot> LoadAsync(Guid propertyId, DateOnly period, Guid[]? roomIds, CancellationToken ct,
        bool includeUnoccupied = false)
    {
        var periodEnd = period.AddMonths(1).AddDays(-1);
        var roomQuery = db.Rooms.AsNoTracking().Where(x => x.PropertyId == propertyId);
        if (roomIds is null)
            roomQuery = roomQuery.Where(room => db.Contracts.Any(x => x.RoomId == room.Id &&
                OccupancyStatuses.Contains(x.Status) && x.StartDate <= periodEnd &&
                (x.HandoverOn ?? x.EndDate ?? DateOnly.MaxValue) >= period) ||
                includeUnoccupied && room.Status != RoomStatus.Inactive &&
                (room.OpeningElectricity == null && !db.MeterReadings.Any(x => x.RoomId == room.Id && x.MeterType == UtilityType.Electricity) ||
                 room.OpeningWater == null && !db.MeterReadings.Any(x => x.RoomId == room.Id && x.MeterType == UtilityType.Water)));
        else
            roomQuery = roomQuery.Where(x => roomIds.Contains(x.Id));
        var rooms = await roomQuery.OrderBy(x => x.RoomNumber).ThenBy(x => x.Id)
            .Take(RoomManagement.Business.Services.MeterImportService.MaxRows + 1).ToDictionaryAsync(x => x.Id, ct);
        var ids = rooms.Keys.ToArray();
        var contracts = await db.Contracts.AsNoTracking().Where(x => ids.Contains(x.RoomId) &&
            OccupancyStatuses.Contains(x.Status)).ToListAsync(ct);
        var previous = period.AddMonths(-1);
        var metersWithHistory = await db.MeterReadings.AsNoTracking()
            .Where(x => ids.Contains(x.RoomId))
            .Select(x => new { x.RoomId, x.MeterType }).Distinct().ToListAsync(ct);
        var readings = await db.MeterReadings.AsNoTracking().Where(x => ids.Contains(x.RoomId) &&
            (x.Year > previous.Year || x.Year == previous.Year && x.Month >= previous.Month ||
                x.ContractId != null && db.Contracts.Any(c => c.Id == x.ContractId &&
                    c.HandoverOn != null && c.HandoverOn < period &&
                    c.HandoverOn.Value.Year == x.Year && c.HandoverOn.Value.Month == x.Month))).ToListAsync(ct);
        var invoices = await db.Invoices.AsNoTracking().Where(x => ids.Contains(x.RoomId) &&
            x.InvoiceType == InvoiceType.Monthly && x.BillingYear == period.Year && x.BillingMonth == period.Month &&
            x.Status != InvoiceStatus.Cancelled).ToListAsync(ct);
        return new Snapshot(rooms, contracts.ToLookup(x => x.RoomId), readings.ToLookup(x => x.RoomId),
            invoices.ToLookup(x => x.RoomId), metersWithHistory.Select(x => (x.RoomId, x.MeterType)).ToHashSet());
    }

    private static (MeterImportRow Row, Contract? Contract, Invoice? Invoice) Assess(
        Snapshot snapshot, DateOnly period, MeterImportRowInput input, bool committing)
    {
        if (!snapshot.Rooms.TryGetValue(input.RoomId, out var room))
            return (Unavailable(input, "", "Không tìm thấy phòng đủ điều kiện trong khu trọ."), null, null);
        var contracts = snapshot.Contracts[room.Id].ToList();
        var current = contracts.Where(x => Overlaps(x, period)).ToList();
        if (input.ContractId == Guid.Empty)
            return (Unavailable(input, room.RoomNumber, "Mã hợp đồng không hợp lệ.", "METER_IMPORT_CONTRACT_INVALID"), null, null);
        if (input.ContractId is null && current.Count > 1)
            return (Unavailable(input, room.RoomNumber, "Phòng có nhiều hợp đồng trong kỳ. Nhập chỉ số theo từng hợp đồng.",
                "METER_IMPORT_MULTIPLE_CONTRACTS"), null, null);
        var contract = input.ContractId is { } contractId ? current.SingleOrDefault(x => x.Id == contractId) : current.SingleOrDefault();
        if (input.ContractId is not null && contract is null)
            return (Unavailable(input, room.RoomNumber, "Hợp đồng không thuộc phòng hoặc không có hiệu lực trong kỳ.", "METER_IMPORT_CONTRACT_INVALID"), null, null);
        var invoices = snapshot.Invoices[room.Id].Where(x => contract is not null && x.ContractId == contract.Id).ToList();
        var blockedInvoice = invoices.Any(x => x.Status != InvoiceStatus.Draft) || invoices.Count > 1;
        var invoice = blockedInvoice ? null : invoices.SingleOrDefault();
        var readings = snapshot.Readings[room.Id].ToList();
        var today = VietnamDate.Today;
        var currentMonth = new DateOnly(today.Year, today.Month, 1);
        return (new MeterImportRow(room.Id, room.RoomNumber, null,
            Cell(UtilityType.Electricity, input.Electricity, input.ExpectedElectricityPrevious, input.ExpectedElectricityCurrent, input.InitialElectricityPrevious),
            Cell(UtilityType.Water, input.Water, input.ExpectedWaterPrevious, input.ExpectedWaterCurrent, input.InitialWaterPrevious), contract?.Id, contract?.ContractCode), contract, invoice);

        MeterImportCell Cell(UtilityType type, decimal? value, decimal? expected, decimal? expectedCurrent, decimal? opening)
        {
            var meter = readings.Where(x => x.MeterType == type).ToList();
            var existing = contract is null ? null : At(meter, period, contract.Id, allowCommon: current.Count == 1);
            if (existing is not null && opening is null && (value is null || value == existing.CurrentValue))
                return new MeterImportCell("AlreadyRecorded", existing.PreviousValue, existing.CurrentValue,
                    "METER_IMPORT_ALREADY_RECORDED", "Đã ghi chỉ số cho kỳ này.", existing.Id);
            var hasHistory = snapshot.MetersWithHistory.Contains((room.Id, type));
            var storedOpening = room.OpeningMeterValue(type);
            var baseline = existing?.PreviousValue ??
                (contract is null ? null : StartingReading(meter, contracts, contract, period)?.CurrentValue) ??
                (hasHistory ? null : storedOpening);
            var canEnterOpening = false;
            MeterImportCell Result(string status, string? code = null, string? message = null) =>
                new(status, baseline, value, code, message, existing?.Id, existing?.CurrentValue, canEnterOpening);

            if (blockedInvoice)
                return Result("NeedsReview", "METER_IMPORT_INVOICE_LOCKED",
                    "Kỳ này có hóa đơn đã phát hành hoặc có hóa đơn cần kiểm tra.");
            if (contract?.Status == ContractStatus.PendingMoveOut && period >= currentMonth)
                return Result("NeedsReview", "METER_IMPORT_HANDOVER_PENDING",
                    "Phòng đang chờ bàn giao. Hãy ghi chỉ số cuối trong luồng trả phòng.");
            canEnterOpening = baseline is null && !hasHistory && room.Status != RoomStatus.Inactive &&
                (contract is null || !contracts.Any(x => x.Id != contract.Id && x.StartDate < contract.StartDate));
            if (opening is not null)
            {
                if (!canEnterOpening)
                    return Result("Error", "METER_IMPORT_INITIAL_NOT_ALLOWED",
                        "Công tơ đã có lịch sử. Hãy tải lại chỉ số và kiểm tra số đầu kỳ.");
                if (opening < 0 || opening > MaxValue || opening != decimal.Round(opening.Value, 2))
                    return Result("Error", "METER_IMPORT_INITIAL_INVALID",
                        "Số đầu kỳ phải không âm và có tối đa hai chữ số thập phân.");
                baseline = opening;
            }
            if (value is null && opening is not null)
                return Result("Ready", message: "Sẽ lưu chỉ số đầu kỳ. Nhập chỉ số cuối kỳ khi chốt điện nước.");
            if (baseline is null)
                return canEnterOpening
                    ? Result("NeedsReview", "METER_IMPORT_INITIAL_REQUIRED", "Ghi lần đầu. Nhập số đầu kỳ thực tế.")
                    : Result("NeedsReview", "METER_IMPORT_PREVIOUS_MISSING",
                        "Chưa xác định được chỉ số đầu kỳ. Kiểm tra chỉ số kỳ trước hoặc chỉ số bàn giao.");
            if (value is null)
                return Result("Missing", "METER_IMPORT_VALUE_MISSING", "Chưa nhập.");
            if (contract is null)
                return Result("NeedsReview", "METER_IMPORT_NOT_OCCUPIED",
                    "Phòng chưa có hợp đồng trong kỳ. Chỉ có thể nhập riêng chỉ số đầu kỳ lần đầu.");
            if ((committing || expected is not null) && expected != baseline ||
                committing && expectedCurrent != existing?.CurrentValue)
                return Result("Error", "METER_IMPORT_BASELINE_CHANGED",
                    "Chỉ số trên hệ thống khác với bản đã kiểm tra. Hãy kiểm tra lại trước khi lưu.");
            if (value < baseline || value > MaxValue || value != decimal.Round(value.Value, 2))
                return Result("Error", "METER_IMPORT_VALUE_INVALID",
                    "Chỉ số cuối phải từ chỉ số đầu trở lên và có tối đa hai chữ số thập phân.");
            var next = meter.Where(x => InPeriod(x) > period && (x.ContractId == contract.Id ||
                x.ContractId is null && Overlaps(contract, InPeriod(x)))).OrderBy(InPeriod)
                .ThenByDescending(x => x.ContractId.HasValue).FirstOrDefault();
            if (next is not null && (InPeriod(next) != period.AddMonths(1) || next.PreviousValue != value))
                return Result("Error", "METER_IMPORT_NEXT_READING_GAP",
                    InPeriod(next) != period.AddMonths(1)
                        ? $"Thiếu chỉ số kỳ {period.AddMonths(1):MM/yyyy} trước kỳ đã ghi {InPeriod(next):MM/yyyy}."
                        : $"Chỉ số cuối {value:0.##} không khớp chỉ số đầu {next.PreviousValue:0.##} của kỳ {InPeriod(next):MM/yyyy} đã ghi.");
            if (existing is not null && BreaksHandover(meter, contracts, contract, existing, value.Value))
                return Result("Error", "METER_IMPORT_NEXT_READING_GAP",
                    "Chỉ số này đã được dùng làm số đầu của lượt thuê sau.");
            if (existing is null && contract.HandoverOn is { } handover &&
                handover.Year == period.Year && handover.Month == period.Month &&
                contracts.Where(x => x.StartDate > handover).Any(successor =>
                {
                    var predecessor = contracts.Where(x => x.Id != successor.Id && x.StartDate < successor.StartDate)
                        .OrderByDescending(x => x.StartDate).FirstOrDefault();
                    var start = new DateOnly(successor.StartDate.Year, successor.StartDate.Month, 1);
                    var shared = contracts.Any(x => x.Id != successor.Id && Overlaps(x, start));
                    var first = At(meter, start, successor.Id, allowCommon: !shared);
                    return predecessor?.Id == contract.Id && first is not null && first.PreviousValue != value;
                }))
                return Result("Error", "METER_IMPORT_NEXT_READING_GAP",
                    "Chỉ số này không khớp số đầu đã ghi của lượt thuê sau.");
            return Result("Ready", message: invoice is null ? null :
                "Chỉ số này sẽ cập nhật tiền điện nước trong hóa đơn nháp.");
        }
    }

    private static bool BreaksHandover(List<MeterReading> meter, List<Contract> contracts, Contract contract,
        MeterReading existing, decimal proposed)
    {
        if (existing.ContractId != contract.Id || contract.HandoverOn is not { } handover ||
            existing.Year != handover.Year || existing.Month != handover.Month)
            return false;
        foreach (var successor in contracts.Where(x => x.StartDate > handover))
        {
            var start = new DateOnly(successor.StartDate.Year, successor.StartDate.Month, 1);
            if (StartingReading(meter, contracts, successor, start)?.Id != existing.Id)
                continue;
            var shared = contracts.Any(x => x.Id != successor.Id && Overlaps(x, start));
            if (At(meter, start, successor.Id, allowCommon: !shared) is { } first && first.PreviousValue != proposed)
                return true;
        }
        return false;
    }

    private static MeterReading? StartingReading(List<MeterReading> meter, List<Contract> contracts,
        Contract contract, DateOnly period)
    {
        var previousMonth = period.AddMonths(-1);
        if (contract.StartDate < period)
        {
            var shared = contracts.Any(x => x.Id != contract.Id && Overlaps(x, previousMonth));
            return At(meter, previousMonth, contract.Id, !shared);
        }

        // When a new tenancy starts, the immediately preceding tenant is the only defensible handover source.
        var previous = contracts.Where(x => x.Id != contract.Id && x.StartDate < contract.StartDate)
            .OrderByDescending(x => x.StartDate).ToList();
        if (previous.Count == 0)
            return At(meter, previousMonth, contract.Id, allowCommon: true);
        var predecessor = previous[0];
        if (predecessor.HandoverOn is not { } handover || handover >= contract.StartDate ||
            previous.Skip(1).Any(x => x.StartDate == predecessor.StartDate ||
                (x.HandoverOn ?? x.EndDate ?? DateOnly.MaxValue) >= handover))
            return null;
        return At(meter, new DateOnly(handover.Year, handover.Month, 1), predecessor.Id, allowCommon: false);
    }

    private static bool Overlaps(Contract contract, DateOnly period) =>
        contract.StartDate < period.AddMonths(1) && (contract.HandoverOn ?? contract.EndDate ?? DateOnly.MaxValue) >= period;

    private static DateOnly InPeriod(MeterReading reading) => new(reading.Year, reading.Month, 1);

    private static MeterReading? At(IEnumerable<MeterReading> readings, DateOnly period, Guid contractId, bool allowCommon) =>
        readings.Where(x => x.Year == period.Year && x.Month == period.Month &&
            (x.ContractId == contractId || allowCommon && x.ContractId is null))
            .OrderByDescending(x => x.ContractId.HasValue).FirstOrDefault();

    private static bool IsOpeningOnly(MeterImportCell cell) => cell.Status == "Ready" && cell.CurrentValue is null;

    private static MeterReading? NewReading(MeterImportCell cell, Guid roomId, Guid? contractId, DateOnly period,
        UtilityType type, OperatorContext actor, bool manual) => cell.Status == "Ready" &&
        cell.CurrentValue is not null && contractId.HasValue ? new MeterReading
        {
            Id = cell.ReadingId ?? Guid.NewGuid(), RoomId = roomId, ContractId = contractId, MeterType = type,
            Year = (short)period.Year, Month = (byte)period.Month,
            PreviousValue = cell.PreviousValue!.Value, CurrentValue = cell.CurrentValue!.Value,
            RecordedAt = DateTime.UtcNow,
            Note = $"{(manual ? "MeterImportManual" : "MeterImportExcel")} - {actor.ActorUserId}"
        } : null;

    private static MeterImportRow Unavailable(MeterImportRowInput input, string name, string message,
        string code = "METER_IMPORT_ROOM_UNAVAILABLE") => new(input.RoomId, name, null,
        new("NeedsReview", null, input.Electricity, code, message),
        new("NeedsReview", null, input.Water, code, message), input.ContractId);

    private static MeterImportCell ApplyDraftError(MeterImportCell cell,
        Dictionary<UtilityType, (string Code, string Message)> errors, UtilityType type) =>
        errors.TryGetValue(type, out var error) ? Fail(cell, error.Code, error.Message) : cell;

    private static MeterImportCell Fail(MeterImportCell cell, string code, string message) =>
        cell with { Status = "Error", Code = code, Message = message };

    private static MeterImportCell Saved(MeterImportCell cell, MeterReading? reading, bool openingOnly) =>
        cell.Status == "Ready" && (reading is not null || openingOnly) ? cell with
        {
            Status = "Saved", ReadingId = reading?.Id, CanEnterOpening = false,
            Message = openingOnly ? "Đã lưu chỉ số đầu kỳ." : "Đã lưu chỉ số."
        } : cell;

    private sealed record Snapshot(Dictionary<Guid, Room> Rooms, ILookup<Guid, Contract> Contracts,
        ILookup<Guid, MeterReading> Readings, ILookup<Guid, Invoice> Invoices,
        HashSet<(Guid RoomId, UtilityType Type)> MetersWithHistory);
}
