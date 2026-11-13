using RoomManagement.Business.Errors;
using RoomManagement.Business.Services;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;
using Xunit;

namespace RoomManagement.Business.Tests;

public sealed class MonthlyInvoiceCalculatorTests
{
    private readonly MonthlyInvoiceCalculator _calculator = new();

    [Fact]
    public void Full_month_snapshots_rent_utilities_and_service()
    {
        var roomId = Guid.NewGuid();
        var contract = Contract(roomId, new DateOnly(2026, 1, 1), 3_000_000m, 5);
        var result = _calculator.Calculate(contract, 2026, 10, new DateOnly(2026, 11, 1),
            Reading(roomId, UtilityType.Electricity, 2026, 10, 100, 150),
            Rate(UtilityType.Electricity, 3_500),
            Reading(roomId, UtilityType.Water, 2026, 10, 20, 30),
            Rate(UtilityType.Water, 15_000),
            [new(Guid.NewGuid(), "Internet", 100_000, 1)]);

        Assert.Equal(new DateOnly(2026, 11, 5), result.DueDate);
        Assert.Equal(3_425_000m, result.TotalAmount);
        Assert.Equal(4, result.Items.Count);
        Assert.Equal(175_000m, result.Items.Single(x => x.Type == InvoiceItemType.Electricity).Amount);
    }

    [Fact]
    public void Partial_month_with_meter_usage_requires_handover_reading()
    {
        var roomId = Guid.NewGuid();
        var contract = Contract(roomId, new DateOnly(2026, 4, 16), 3_000_000m, 5);
        var error = Assert.Throws<BusinessException>(() => _calculator.Calculate(
            contract, 2026, 4, new DateOnly(2026, 5, 1),
            Reading(roomId, UtilityType.Electricity, 2026, 4, 100, 110),
            Rate(UtilityType.Electricity, 3_500),
            Reading(roomId, UtilityType.Water, 2026, 4, 20, 20),
            Rate(UtilityType.Water, 15_000), []));
        Assert.Equal("INVOICE_PARTIAL_METER_UNSUPPORTED", error.Code);
    }

    [Fact]
    public void Partial_month_without_meter_usage_prorates_rent()
    {
        var roomId = Guid.NewGuid();
        var contract = Contract(roomId, new DateOnly(2026, 4, 16), 3_000_000m, 31);
        var result = _calculator.Calculate(contract, 2026, 4, new DateOnly(2026, 5, 1),
            Reading(roomId, UtilityType.Electricity, 2026, 4, 100, 100),
            Rate(UtilityType.Electricity, 3_500),
            Reading(roomId, UtilityType.Water, 2026, 4, 20, 20),
            Rate(UtilityType.Water, 15_000), []);
        Assert.Equal(1_500_000m, result.TotalAmount);
        Assert.Equal(new DateOnly(2026, 5, 31), result.DueDate);
        Assert.Single(result.Items);
    }

    [Fact]
    public void Passed_due_date_moves_to_issue_date_for_late_billing()
    {
        var roomId = Guid.NewGuid();
        var contract = Contract(roomId, new DateOnly(2026, 1, 1), 3_000_000m, 5);
        var result = _calculator.Calculate(
            contract, 2026, 1, new DateOnly(2026, 3, 1),
            Reading(roomId, UtilityType.Electricity, 2026, 1, 0, 0),
            Rate(UtilityType.Electricity, 3_500),
            Reading(roomId, UtilityType.Water, 2026, 1, 0, 0),
            Rate(UtilityType.Water, 15_000), []);
        Assert.Equal(new DateOnly(2026, 3, 1), result.DueDate);
    }

    private static Contract Contract(Guid roomId, DateOnly start, decimal rent, byte dueDay) =>
        new() { Id = Guid.NewGuid(), RoomId = roomId, StartDate = start,
            MonthlyRent = rent, PaymentDueDay = dueDay };

    private static MeterReading Reading(Guid roomId, UtilityType type, short year, byte month,
        decimal previous, decimal current) =>
        new() { Id = Guid.NewGuid(), RoomId = roomId, MeterType = type,
            Year = year, Month = month, PreviousValue = previous, CurrentValue = current };

    private static UtilityRate Rate(UtilityType type, decimal price) =>
        new() { UtilityType = type, UnitPrice = price };
}
