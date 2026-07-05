using Tsumugi.Domain.Entities;

namespace Tsumugi.Domain.Logic;

public enum AverageWageDenominator
{
    TotalRecipients = 0,
    ActiveRecipients = 1,
}

/// <summary>
/// 平均工賃月額算定クラス。
/// AC2-8 の正式定義（厚労省告示/通知）確定時は、<see cref="AverageWageDenominator"/> の列挙値追加
/// （例：常勤換算・除外者を含む定義）と <see cref="Calculate"/> のオプション引数追加で対応する。
/// 本構造は分母切替に強いよう設計されている。
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
