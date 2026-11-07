using RoomManagement.Business.DTOs;
using RoomManagement.Business.Services;

namespace RoomManagement.Tests;

public sealed class ContractTenureCalculatorTests
{
    [Fact]
    public void Seven_uncovered_days_join_contract_periods_and_count_in_calendar_tenure()
    {
        var spans = ContractTenureCalculator.Calculate([
            new(new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 31)),
            new(new DateOnly(2026, 2, 8), new DateOnly(2026, 2, 28))
        ]);

        var span = Assert.Single(spans);
        Assert.Equal(new DateOnly(2026, 1, 10), span.From);
        Assert.Equal(new DateOnly(2026, 2, 28), span.To);
        Assert.Equal(50, span.CalendarDays);
        Assert.Equal(43, span.ContractDays);
    }

    [Fact]
    public void Eight_uncovered_days_start_a_new_stay()
    {
        var spans = ContractTenureCalculator.Calculate([
            new(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31)),
            new(new DateOnly(2026, 2, 9), new DateOnly(2026, 2, 28))
        ]);

        Assert.Equal(2, spans.Count);
        Assert.Equal(31, spans[0].CalendarDays);
        Assert.Equal(20, spans[1].CalendarDays);
    }

    [Fact]
    public void Overlapping_contract_periods_do_not_double_count_covered_days()
    {
        var span = Assert.Single(ContractTenureCalculator.Calculate([
            new(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31)),
            new(new DateOnly(2026, 1, 20), new DateOnly(2026, 2, 10))
        ]));

        Assert.Equal(41, span.CalendarDays);
        Assert.Equal(41, span.ContractDays);
    }
}
