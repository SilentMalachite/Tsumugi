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
}
