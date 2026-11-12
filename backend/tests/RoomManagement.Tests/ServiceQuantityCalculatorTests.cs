using RoomManagement.Business.DTOs;
using RoomManagement.Business.Services;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Tests;

public sealed class ServiceQuantityCalculatorTests
{
    [Fact]
    public void Monthly_invoice_snapshots_manually_entered_service_quantity()
    {
        var roomId = Guid.NewGuid();
        var contract = new Contract { Id = Guid.NewGuid(), RoomId = roomId,
            StartDate = new DateOnly(2025, 1, 1), MonthlyRent = 1_000_000m, PaymentDueDay = 10 };
        var electricity = new MeterReading { RoomId = roomId, MeterType = UtilityType.Electricity,
            Year = 2026, Month = 1, PreviousValue = 0, CurrentValue = 0 };
        var water = new MeterReading { RoomId = roomId, MeterType = UtilityType.Water,
            Year = 2026, Month = 1, PreviousValue = 0, CurrentValue = 0 };
        var electricityRate = new UtilityRate { UtilityType = UtilityType.Electricity, UnitPrice = 3_000m };
        var waterRate = new UtilityRate { UtilityType = UtilityType.Water, UnitPrice = 10_000m };
        var serviceId = Guid.NewGuid();

        var result = new MonthlyInvoiceCalculator().Calculate(contract, 2026, 1,
            new DateOnly(2026, 1, 15), electricity, electricityRate, water, waterRate,
            [new BillableRoomService(serviceId, "Gửi xe", 80_000m, 3)]);

        var item = Assert.Single(result.Items, x => x.Type == InvoiceItemType.Service);
        Assert.Equal(3, item.Quantity);
        Assert.Equal(80_000m, item.UnitPrice);
        Assert.Equal(240_000m, item.Amount);
        Assert.Equal(serviceId, item.ReferenceId);
        Assert.Equal(1_240_000m, result.TotalAmount);
    }
}
