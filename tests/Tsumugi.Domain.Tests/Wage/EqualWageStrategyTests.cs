using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Wage;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests.Wage;

public sealed class EqualWageStrategyTests
{
    private static readonly Guid Office = Guid.NewGuid();
    private static readonly DateTimeOffset T = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    private static WageSettings Settings() => WageSettings.Create(
        Guid.NewGuid(), Office, new DateRange(new DateOnly(2026, 4, 1), null),
        WageMethod.Equal, RoundingRule.FloorYen, RemainderPolicy.LargestRemainder, 4, null,
        workAllowancePerDayYen: null, skillAllowanceTiers: null, hourUnitMinutes: 15, "t", T);

    private static WageSettings SettingsWithAllowances() => WageSettings.Create(
        Guid.NewGuid(), Office, new DateRange(new DateOnly(2026, 4, 1), null),
        WageMethod.Equal, RoundingRule.FloorYen, RemainderPolicy.LargestRemainder, 4, null,
        workAllowancePerDayYen: 500,
        skillAllowanceTiers: new[] { new SkillAllowanceTier(55, 2000) },
        hourUnitMinutes: 15, "t", T);

    private static WageFund Fund(int yen) =>
        WageFund.NewRecord(Guid.NewGuid(), Office, new YearMonth(2026, 7), yen, null, "t", T);

    [Fact]
    public void Splits_equally_among_present_recipients()
    {
        var inputs = new[]
        {
            new WageInputs(Guid.NewGuid(), 10, 0, 0, 0),
            new WageInputs(Guid.NewGuid(), 5, 0, 0, 0),
            new WageInputs(Guid.NewGuid(), 0, 0, 0, 0),
        };
        var lines = new EqualWageStrategy().Calculate(inputs, Fund(100), Settings());
        lines.Sum(l => l.AmountYen).Should().Be(100);
        lines.Where(l => l.AmountYen > 0).Should().HaveCount(2);
        lines.Where(l => l.AmountYen > 0).Select(l => l.AmountYen).Should().AllBeEquivalentTo(50);
        lines.First(l => l.RecipientId == inputs[2].RecipientId).AmountYen.Should().Be(0);
    }

    [Fact]
    public void All_absent_with_zero_fund_yields_all_zero()
    {
        var inputs = new[]
        {
            new WageInputs(Guid.NewGuid(), 0, 0, 0, 0),
            new WageInputs(Guid.NewGuid(), 0, 0, 0, 0),
        };
        var lines = new EqualWageStrategy().Calculate(inputs, Fund(0), Settings());
        lines.Sum(l => l.AmountYen).Should().Be(0);
        lines.Should().AllSatisfy(l => l.AmountYen.Should().Be(0));
    }

    [Fact]
    public void Adds_work_and_skill_allowance_on_top_of_equal_split()
    {
        // 出席 20 日・60h(=3600分) の利用者: 均等割 100 + 作業手当 20*500=10000 + 職能手当 2000
        var inputs = new[]
        {
            new WageInputs(Guid.NewGuid(), PresentDays: 20, TotalWorkedMinutes: 3600, 0, 0),
        };
        var lines = new EqualWageStrategy().Calculate(inputs, Fund(100), SettingsWithAllowances());
        lines.Single().AmountYen.Should().Be(100 + 10_000 + 2_000);
    }

    [Fact]
    public void All_absent_with_positive_fund_throws_to_preserve_sigma_invariant()
    {
        var inputs = new[]
        {
            new WageInputs(Guid.NewGuid(), 0, 0, 0, 0),
            new WageInputs(Guid.NewGuid(), 0, 0, 0, 0),
        };
        var act = () => new EqualWageStrategy().Calculate(inputs, Fund(50_000), Settings());
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("配分対象の総重みが 0 のため、原資 50,000 円を最大剰余法で配分できません。事業所留保へ切り替えるか、原資を 0 円に設定してください。");
    }
}
