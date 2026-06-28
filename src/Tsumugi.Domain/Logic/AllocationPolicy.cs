using Tsumugi.Domain.Enums;

namespace Tsumugi.Domain.Logic;

/// <summary>配分の端数・余りを決定的に処理する純粋関数。Σ AmountYen == totalYen を保証する。</summary>
public static class AllocationPolicy
{
    public static IReadOnlyList<(Guid Key, int AmountYen)> Allocate(
        IReadOnlyList<(Guid Key, decimal Weight)> shares,
        int totalYen,
        RoundingRule rounding,
        RemainderPolicy remainder,
        Guid? officeReserveKey = null)
    {
        ArgumentNullException.ThrowIfNull(shares);
        ArgumentOutOfRangeException.ThrowIfNegative(totalYen);
        if (remainder == RemainderPolicy.ReserveToOffice && officeReserveKey is null)
            throw new ArgumentException("ReserveToOffice では officeReserveKey が必要です。", nameof(officeReserveKey));
        if (shares.Count == 0) return Array.Empty<(Guid, int)>();

        var totalWeight = shares.Sum(s => s.Weight);
        if (totalWeight <= 0m)
            return shares.Select(s => (s.Key, 0)).ToArray();

        var raw = shares
            .Select(s => (s.Key, Exact: (decimal)totalYen * s.Weight / totalWeight))
            .ToArray();

        var floored = raw.Select(r => Round(r.Exact, rounding)).ToArray();
        var allocated = floored.Sum();
        var leftover = totalYen - allocated;

        if (leftover == 0)
            return raw.Zip(floored, (r, f) => (r.Key, f)).ToList();

        if (remainder == RemainderPolicy.ReserveToOffice)
        {
            var officeIndex = Array.FindIndex(raw, t => t.Key == officeReserveKey!.Value);
            if (officeIndex < 0)
            {
                var result = raw.Zip(floored, (r, f) => (r.Key, AmountYen: f)).ToList();
                result.Add((officeReserveKey!.Value, leftover));
                return result;
            }
            floored[officeIndex] += leftover;
            return raw.Zip(floored, (r, f) => (r.Key, f)).ToList();
        }

        var ordered = raw
            .Select((r, i) => (Index: i, r.Key, Fraction: r.Exact - floored[i]))
            .OrderByDescending(t => t.Fraction)
            .ThenBy(t => t.Key)
            .ToArray();

        var step = leftover > 0 ? 1 : -1;
        var remaining = Math.Abs(leftover);
        for (var i = 0; i < remaining; i++)
        {
            floored[ordered[i % ordered.Length].Index] += step;
        }

        return raw.Zip(floored, (r, f) => (r.Key, f)).ToList();
    }

    private static int Round(decimal exact, RoundingRule rule) => rule switch
    {
        RoundingRule.FloorYen => (int)Math.Floor(exact),
        RoundingRule.CeilYen => (int)Math.Ceiling(exact),
        RoundingRule.RoundHalfAwayFromZeroYen => (int)Math.Round(exact, MidpointRounding.AwayFromZero),
        _ => throw new ArgumentOutOfRangeException(nameof(rule), rule, "未対応の RoundingRule"),
    };
}
