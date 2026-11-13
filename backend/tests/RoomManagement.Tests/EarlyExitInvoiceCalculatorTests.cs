using RoomManagement.Business.DTOs;
using RoomManagement.Business.Errors;
using RoomManagement.Business.Services;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Tests;

public sealed class EarlyExitInvoiceCalculatorTests
{
    [Fact]
    public void Final_month_uses_inclusive_handover_day_and_contract_meter_segments()
    {
        var roomId = Guid.NewGuid();
        var contract = new Contract { Id = Guid.NewGuid(), RoomId = roomId,
            StartDate = new DateOnly(2026, 11, 1), HandoverOn = new DateOnly(2026, 11, 13),
            Status = ContractStatus.PendingSettlement, MonthlyRent = 3_000_000m, PaymentDueDay = 10 };
        var electricity = new MeterReading { RoomId = roomId, ContractId = contract.Id,
            MeterType = UtilityType.Electricity, Year = 2026, Month = 11,
            PreviousValue = 100, CurrentValue = 112 };
        var water = new MeterReading { RoomId = roomId, ContractId = contract.Id,
            MeterType = UtilityType.Water, Year = 2026, Month = 11,
            PreviousValue = 20, CurrentValue = 22 };
        var result = new MonthlyInvoiceCalculator().Calculate(contract, 2026, 11,
            new DateOnly(2026, 12, 20), electricity,
            new UtilityRate { UtilityType = UtilityType.Electricity, UnitPrice = 3_000 },
            water, new UtilityRate { UtilityType = UtilityType.Water, UnitPrice = 10_000 },
            [new BillableRoomService(Guid.NewGuid(), "Internet", 100_000, 1)]);

        Assert.Equal(1_300_000m, Assert.Single(result.Items, x => x.Type == InvoiceItemType.Rent).Amount);
        Assert.Equal(36_000m, Assert.Single(result.Items, x => x.Type == InvoiceItemType.Electricity).Amount);
        Assert.Equal(20_000m, Assert.Single(result.Items, x => x.Type == InvoiceItemType.Water).Amount);
        Assert.Equal(100_000m, Assert.Single(result.Items, x => x.Type == InvoiceItemType.Service).Amount);
        Assert.Equal(1_456_000m, result.TotalAmount);
        Assert.Equal(new DateOnly(2026, 12, 20), result.DueDate);
    }

    [Fact]
    public void Partial_month_with_room_wide_meter_reading_remains_blocked()
    {
        var roomId = Guid.NewGuid();
        var contract = new Contract { Id = Guid.NewGuid(), RoomId = roomId,
            StartDate = new DateOnly(2026, 11, 1), HandoverOn = new DateOnly(2026, 11, 13),
            Status = ContractStatus.PendingSettlement, MonthlyRent = 3_000_000m, PaymentDueDay = 10 };
        var electricity = new MeterReading { RoomId = roomId, MeterType = UtilityType.Electricity,
            Year = 2026, Month = 11, PreviousValue = 100, CurrentValue = 112 };
        var water = new MeterReading { RoomId = roomId, MeterType = UtilityType.Water,
            Year = 2026, Month = 11, PreviousValue = 20, CurrentValue = 22 };

        var error = Assert.Throws<BusinessException>(() => new MonthlyInvoiceCalculator().Calculate(contract,
            2026, 11, new DateOnly(2026, 12, 1), electricity,
            new UtilityRate { UtilityType = UtilityType.Electricity, UnitPrice = 3_000 },
            water, new UtilityRate { UtilityType = UtilityType.Water, UnitPrice = 10_000 }, []));
        Assert.Equal("INVOICE_PARTIAL_METER_UNSUPPORTED", error.Code);
    }
}
