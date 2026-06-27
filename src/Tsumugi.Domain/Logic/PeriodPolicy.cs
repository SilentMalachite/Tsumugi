using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Logic;

/// <summary>期間の重複・空白検出（純粋関数）。</summary>
public static class PeriodPolicy
{
    public static IReadOnlyList<(int LeftIndex, int RightIndex)> DetectOverlaps(IEnumerable<DateRange> ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        var arr = ranges.ToArray();
        var result = new List<(int, int)>();
        for (var i = 0; i < arr.Length; i++)
            for (var j = i + 1; j < arr.Length; j++)
                if (arr[i].Overlaps(arr[j])) result.Add((i, j));
        return result;
    }

    public static IReadOnlyList<DateRange> DetectGaps(IEnumerable<DateRange> ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        var ordered = ranges.OrderBy(r => r.Start).ToArray();
        var result = new List<DateRange>();
        for (var i = 0; i < ordered.Length - 1; i++)
        {
            if (ordered[i].End is not { } end) continue;
            var nextStart = ordered[i + 1].Start;
            var dayAfter = end.AddDays(1);
            if (dayAfter < nextStart)
                result.Add(new DateRange(dayAfter, nextStart.AddDays(-1)));
        }
        return result;
    }
}
