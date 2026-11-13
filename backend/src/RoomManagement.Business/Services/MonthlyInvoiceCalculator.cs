using RoomManagement.Business.DTOs;
using RoomManagement.Business.Errors;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Business.Services;

public sealed class MonthlyInvoiceCalculator
{
    public InvoicePreviewResult Calculate(Contract contract, short year, byte month,
        DateOnly issueDate, MeterReading electricity, UtilityRate electricityRate,
        MeterReading water, UtilityRate waterRate,
        IReadOnlyList<BillableRoomService> services)
    { //kiểm tra tháng năm hợp lệ
        if (year is < 2000 or > 2100 || month is < 1 or > 12)
            throw Validation("INVOICE_PERIOD_INVALID", "Kỳ hóa đơn không hợp lệ.");

        var periodStart = new DateOnly(year, month, 1);
        var periodEnd = new DateOnly(year, month, DateTime.DaysInMonth(year, month));

        if (contract.StartDate > periodEnd || contract.EndDate is { } end && end < periodStart)
            throw Validation("CONTRACT_OUTSIDE_PERIOD", "Hợp đồng không thuộc kỳ hóa đơn.");

        if (contract.PaymentDueDay is null)
            throw Validation("INVOICE_DUE_DAY_MISSING", "Hợp đồng chưa có ngày đến hạn thanh toán.");
        //kiểm tra các chỉ số / đơn giá hợp lệ
        if (electricity.RoomId != contract.RoomId || water.RoomId != contract.RoomId ||
            electricity.MeterType != UtilityType.Electricity || water.MeterType != UtilityType.Water ||
            electricity.Year != year || electricity.Month != month ||
            water.Year != year || water.Month != month ||
            electricityRate.UtilityType != UtilityType.Electricity || waterRate.UtilityType != UtilityType.Water)
            throw Validation("INVOICE_INPUT_MISMATCH", "Chỉ số hoặc đơn giá không khớp kỳ hóa đơn.");

        var first = contract.StartDate > periodStart ? contract.StartDate : periodStart;
        var last = contract.EndDate is { } contractEnd && contractEnd < periodEnd ? contractEnd : periodEnd;
        if (contract.HandoverOn is { } handover && handover < last) last = handover;
        var days = last.DayNumber - first.DayNumber + 1;

        if (days <= 0) throw Validation("CONTRACT_OUTSIDE_PERIOD", "Hợp đồng không thuộc kỳ hóa đơn.");
        // cần xem xét phần logic này, có nên thêm số ngày cụ thể để được tính là thuê lẻ 
        if (days != DateTime.DaysInMonth(year, month) &&
            (electricity.ContractId != contract.Id || water.ContractId != contract.Id) &&
            (electricity.CurrentValue != electricity.PreviousValue || water.CurrentValue != water.PreviousValue))
            throw Validation("INVOICE_PARTIAL_METER_UNSUPPORTED",
                "Kỳ thuê lẻ tháng có tiêu thụ điện nước nhưng chưa có chỉ số bàn giao để phân bổ.");

        var nextMonth = periodStart.AddMonths(1);
        var dueDate = new DateOnly(nextMonth.Year, nextMonth.Month,
            Math.Min(contract.PaymentDueDay.Value, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month)));

        if (dueDate < issueDate)
        {
            dueDate = issueDate;
        }

        var rentAmount = Money(contract.MonthlyRent * days / DateTime.DaysInMonth(year, month));
        var items = new List<CalculatedInvoiceItem>
        {
            new(InvoiceItemType.Rent, "Tiền phòng", 1, rentAmount, rentAmount, null)
        };

        AddUtility(items, InvoiceItemType.Electricity, "Điện", electricity, electricityRate);
        AddUtility(items, InvoiceItemType.Water, "Nước", water, waterRate);

        foreach (var service in services)
        {
            if (service.UnitPrice < 0 || service.Quantity <= 0) throw Validation("SERVICE_PRICE_INVALID", "Đơn giá hoặc số lượng dịch vụ không hợp lệ.");
            var price = Money(service.UnitPrice);
            decimal amount;
            try { amount = Money(checked(service.Quantity * price)); }
            catch (OverflowException) { throw Validation("SERVICE_AMOUNT_INVALID", "Thành tiền dịch vụ vượt giới hạn."); }
            if (amount > 9_999_999_999_999_999.99m)
                throw Validation("SERVICE_AMOUNT_INVALID", "Thành tiền dịch vụ vượt giới hạn.");
            items.Add(new CalculatedInvoiceItem(InvoiceItemType.Service,
                service.Name, service.Quantity, price, amount, service.ServiceId));
        }

        var total = items.Sum(x => x.Amount);

        if (total < 0) throw Validation("INVOICE_TOTAL_INVALID", "Tổng hóa đơn không hợp lệ.");
        return new InvoicePreviewResult(contract.Id, contract.RoomId, year, month,
            issueDate, dueDate, items, total);
    }

    private static void AddUtility(List<CalculatedInvoiceItem> items,
        InvoiceItemType type, string name, MeterReading reading, UtilityRate rate)
    {
        var used = reading.CurrentValue - reading.PreviousValue;

        if (used < 0 || rate.UnitPrice < 0)
            throw Validation("INVOICE_INPUT_INVALID", "Chỉ số hoặc giá tiện ích không hợp lệ.");

        if (used == 0) return; // InvoiceItems.Quantity must be positive.
        items.Add(new CalculatedInvoiceItem(type, name, used, rate.UnitPrice,
            Money(used * rate.UnitPrice), reading.Id));
    }

    private static decimal Money(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static BusinessException Validation(string code, string message) =>
        new(BusinessErrorType.Validation, code, message);
}
