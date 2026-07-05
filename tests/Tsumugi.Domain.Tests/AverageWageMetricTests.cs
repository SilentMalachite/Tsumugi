using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Logic;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests;

public sealed class AverageWageMetricTests
{
    private static readonly Guid Office = Guid.NewGuid();
    private static readonly DateTimeOffset T = new(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid R1 = Guid.NewGuid();
    private static readonly Guid R2 = Guid.NewGuid();

    private static WageStatement S(Guid recipient, YearMonth month, int yen) =>
        WageStatement.NewRecord(Guid.NewGuid(), Office, month, recipient, yen, "test", "t", T);

    [Fact]
    public void TotalRecipients_uses_sum_count_pairs()
    {
        var stmts = new[]
        {
            S(R1, new YearMonth(2026, 4), 10_000),
            S(R1, new YearMonth(2026, 5), 12_000),
            S(R2, new YearMonth(2026, 4), 8_000),
        };
        AverageWageMetric.Calculate(stmts, AverageWageDenominator.TotalRecipients).Should().Be(10_000);
    }

    [Fact]
    public void ActiveRecipients_uses_distinct_count()
    {
        var stmts = new[]
        {
            S(R1, new YearMonth(2026, 4), 10_000),
            S(R1, new YearMonth(2026, 5), 12_000),
            S(R2, new YearMonth(2026, 4), 8_000),
        };
        AverageWageMetric.Calculate(stmts, AverageWageDenominator.ActiveRecipients).Should().Be(15_000);
    }

    [Fact]
    public void Empty_returns_zero()
        => AverageWageMetric.Calculate(Array.Empty<WageStatement>(), AverageWageDenominator.ActiveRecipients).Should().Be(0);

    /// <summary>
    /// 分母切替の回帰テスト。AC2-8 正式定義確定時は <see cref="AverageWageDenominator"/> を追加し
    /// このテストに対応する <c>[InlineData]</c> を足すだけで対応できることを保証する。
    /// 5 行 (TotalRecipients=5) / 4 名 (ActiveRecipients=4)、合計 10,000 円 で検証。
    /// </summary>
    [Theory]
    [InlineData(AverageWageDenominator.TotalRecipients, 5, 10000, 2000)]
    [InlineData(AverageWageDenominator.ActiveRecipients, 5, 10000, 2500)]
    public void Calculate_switches_denominator(
        AverageWageDenominator denominator, int totalCount, int totalYen, int expected)
    {
        // 5 rows (totalCount) totaling totalYen yen; R1 appears twice → Active=4, Total=5
        var perRow = totalYen / totalCount; // = 2000
        var r3 = Guid.NewGuid();
        var r4 = Guid.NewGuid();
        var stmts = new[]
        {
            S(R1, new YearMonth(2026, 1), perRow),
            S(R1, new YearMonth(2026, 2), perRow),
            S(R2, new YearMonth(2026, 1), perRow),
            S(r3, new YearMonth(2026, 1), perRow),
            S(r4, new YearMonth(2026, 1), perRow),
        };
        AverageWageMetric.Calculate(stmts, denominator).Should().Be(expected);
    }
}
