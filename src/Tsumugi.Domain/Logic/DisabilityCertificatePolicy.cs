using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Domain.Logic;

public sealed record DisabilityCertificateRenewalDue(
    DisabilityCertificate Certificate,
    int RemainingDays);

/// <summary>精神障害者保健福祉手帳の更新期日アラート抽出（純粋関数。日付/I/Oに依存しない）。</summary>
public static class DisabilityCertificatePolicy
{
    /// <summary>
    /// 基準日 <paramref name="asOf"/> 時点で、精神手帳のうち残日数（次回更新日 − 基準日）が
    /// 0 以上 <paramref name="thresholdDays"/> 以下のものを、残日数昇順で返す。
    /// 身体・療育、更新日 null、既に過ぎた更新日（残日数 &lt; 0）は対象外。
    /// </summary>
    public static IReadOnlyList<DisabilityCertificateRenewalDue> FindRenewalDue(
        IEnumerable<DisabilityCertificate> certificates,
        DateOnly asOf,
        int thresholdDays)
    {
        ArgumentNullException.ThrowIfNull(certificates);
        ArgumentOutOfRangeException.ThrowIfNegative(thresholdDays);

        var result = new List<DisabilityCertificateRenewalDue>();
        foreach (var c in certificates)
        {
            if (c.Type != DisabilityCertificateType.Mental) continue;
            if (c.NextRenewalDate is not { } renewal) continue;

            var remaining = renewal.DayNumber - asOf.DayNumber;
            if (remaining >= 0 && remaining <= thresholdDays)
                result.Add(new DisabilityCertificateRenewalDue(c, remaining));
        }

        return result.OrderBy(e => e.RemainingDays).ToArray();
    }
}
