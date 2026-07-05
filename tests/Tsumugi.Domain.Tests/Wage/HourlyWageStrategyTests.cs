using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Wage;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests.Wage;

public sealed class HourlyWageStrategyTests
{
    private static readonly Guid Office = Guid.NewGuid();
    private static readonly YearMonth Month = new(2026, 7);
    private static readonly DateTimeOffset T = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    private static WageSettings Settings() => WageSettings.Create(
        Guid.NewGuid(), Office, new DateRange(new DateOnly(2026, 4, 1), null),
        WageMethod.Hourly, RoundingRule.FloorYen, RemainderPolicy.LargestRemainder, 4, null,
        workAllowancePerDayYen: null, skillAllowanceTiers: null, hourUnitMinutes: 15, "t", T);

    private static WageFund Fund(int yen) =>
        WageFund.NewRecord(Guid.NewGuid(), Office, Month, yen, null, "t", T);

    [Fact]
    public void Distributes_proportional_to_worked_minutes_and_invariant_holds()
    {
        var a = new WageInputs(Guid.NewGuid(), 10, 600, 0, 0);
        var b = new WageInputs(Guid.NewGuid(), 10, 400, 0, 0);
        var lines = new HourlyWageStrategy().Calculate(new[] { a, b }, Fund(100_000), Settings());
        lines.Sum(l => l.AmountYen).Should().Be(100_000);
        lines.First(l => l.RecipientId == a.RecipientId).AmountYen.Should().Be(60_000);
        lines.First(l => l.RecipientId == b.RecipientId).AmountYen.Should().Be(40_000);
    }

    [Fact]
    public void Single_recipient_takes_full_fund()
    {
        var only = new WageInputs(Guid.NewGuid(), 10, 600, 0, 0);
        var lines = new HourlyWageStrategy().Calculate(new[] { only }, Fund(99_991), Settings());
        lines.Single().AmountYen.Should().Be(99_991);
    }

    [Fact]
    public void All_zero_minutes_with_zero_fund_yields_all_zero()
    {
        var a = new WageInputs(Guid.NewGuid(), 0, 0, 0, 0);
        var b = new WageInputs(Guid.NewGuid(), 0, 0, 0, 0);
        var lines = new HourlyWageStrategy().Calculate(new[] { a, b }, Fund(0), Settings());
        lines.Sum(l => l.AmountYen).Should().Be(0);
        lines.Should().AllSatisfy(l => l.AmountYen.Should().Be(0));
    }

    [Fact]
    public void All_zero_minutes_with_positive_fund_throws_to_preserve_sigma_invariant()
    {
        var a = new WageInputs(Guid.NewGuid(), 0, 0, 0, 0);
        var b = new WageInputs(Guid.NewGuid(), 0, 0, 0, 0);
        var act = () => new HourlyWageStrategy().Calculate(new[] { a, b }, Fund(100_000), Settings());
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("配分対象の総重みが 0 のため、原資 100,000 円を最大剰余法で配分できません。事業所留保へ切り替えるか、原資を 0 円に設定してください。");
    }

    [Fact]
    public void No_breakdown_no_fund_no_allowances_yields_zero()
    {
        // DailyBreakdown なし・fund なし・手当なし: 時給分 0 + 手当 0 = 0
        var only = new WageInputs(Guid.NewGuid(), 10, 600, 0, 0);
        var lines = new HourlyWageStrategy().Calculate(new[] { only }, fund: null, Settings());
        lines.Single().AmountYen.Should().Be(0);
    }
}
