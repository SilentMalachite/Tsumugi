using Tsumugi.Domain.Entities;

namespace Tsumugi.Domain.Logic;

public sealed record CertificateExpiry(Certificate Certificate, int RemainingDays);

/// <summary>受給者証の期限アラート抽出（純粋関数。日付/I/Oに依存しない）。</summary>
public static class CertificatePolicy
{
    /// <summary>
    /// 基準日 <paramref name="asOf"/> 時点で、残日数（終了日 − 基準日）が
    /// 0 以上 <paramref name="thresholdDays"/> 以下の受給者証を、残日数昇順で返す。
    /// 終了日 null（無期限）と既に失効（残日数 &lt; 0）は対象外。
    /// </summary>
    public static IReadOnlyList<CertificateExpiry> FindExpiring(
        IEnumerable<Certificate> certificates,
        DateOnly asOf,
        int thresholdDays)
    {
        ArgumentNullException.ThrowIfNull(certificates);
        ArgumentOutOfRangeException.ThrowIfNegative(thresholdDays);

        var result = new List<CertificateExpiry>();
        foreach (var c in certificates)
        {
            if (c.Validity.End is not { } end) continue;
            var remaining = end.DayNumber - asOf.DayNumber;
            if (remaining >= 0 && remaining <= thresholdDays)
                result.Add(new CertificateExpiry(c, remaining));
        }
        return result.OrderBy(e => e.RemainingDays).ToArray();
    }
}
