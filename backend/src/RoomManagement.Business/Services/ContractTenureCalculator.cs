using RoomManagement.Business.DTOs;

namespace RoomManagement.Business.Services;

/// <summary>Tính các chuỗi ở liên tiếp từ ngày thành viên thuộc những hợp đồng đã kích hoạt.</summary>
public static class ContractTenureCalculator
{
    public static IReadOnlyList<ContractTenureSpan> Calculate(IEnumerable<ContractTenurePeriod> periods)
    {
        var ordered = periods.Where(p => p.To >= p.From)
            .OrderBy(p => p.From).ThenBy(p => p.To).ToList();
        var result = new List<ContractTenureSpan>();
        if (ordered.Count == 0) return result;

        var from = ordered[0].From;
        var to = ordered[0].To;
        var coveredDays = to.DayNumber - from.DayNumber + 1;
        foreach (var next in ordered.Skip(1))
        {
            var uncoveredDays = next.From.DayNumber - to.DayNumber - 1;
            if (uncoveredDays > 7)
            {
                result.Add(new ContractTenureSpan(from, to, to.DayNumber - from.DayNumber + 1, coveredDays));
                from = next.From;
                to = next.To;
                coveredDays = to.DayNumber - from.DayNumber + 1;
                continue;
            }

            if (next.To > to)
            {
                coveredDays += next.To.DayNumber - Math.Max(to.DayNumber, next.From.DayNumber - 1);
                to = next.To;
            }
        }
        result.Add(new ContractTenureSpan(from, to, to.DayNumber - from.DayNumber + 1, coveredDays));
        return result;
    }
}
