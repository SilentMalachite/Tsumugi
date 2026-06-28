using Tsumugi.Domain.Entities;

namespace Tsumugi.Domain.Logic;

public enum AverageWageDenominator
{
    TotalRecipients = 0,
    ActiveRecipients = 1,
}

/// <summary>
/// 平均工賃月額（暫定式）。
/// FIXME(open-questions): 正式定義（分母＝延べ/実利用者、対象期間、控除項目）は
/// 厚労省告示/通知突合で確定する。本暫定式は分母の差し替えに強い構造で実装してあり、
/// 確定後は AverageWageDenominator を増減してテストで固定し直すこと。
/// </summary>
public static class AverageWageMetric
{
    public static int Calculate(IReadOnlyList<WageStatement> statements, AverageWageDenominator denominator)
    {
        ArgumentNullException.ThrowIfNull(statements);
        if (statements.Count == 0) return 0;

        var totalYen = statements.Sum(s => (long)s.AmountYen);
        long divisor = denominator switch
        {
            AverageWageDenominator.TotalRecipients => statements.Count,
            AverageWageDenominator.ActiveRecipients => statements.Select(s => s.RecipientId).Distinct().Count(),
            _ => throw new ArgumentOutOfRangeException(nameof(denominator)),
        };
        if (divisor == 0) return 0;
        return (int)(totalYen / divisor);
    }
}
