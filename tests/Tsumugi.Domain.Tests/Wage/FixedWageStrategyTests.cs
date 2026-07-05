using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Wage;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests.Wage;

public sealed class FixedWageStrategyTests
{
    private static WageSettings Settings(int fixedYen) => WageSettings.Create(
        Guid.NewGuid(), Guid.NewGuid(),
        new DateRange(new DateOnly(2026, 4, 1), null),
        WageMethod.Fixed, RoundingRule.FloorYen, RemainderPolicy.LargestRemainder, 4, fixedYen,
        workAllowancePerDayYen: null, skillAllowanceTiers: null, hourUnitMinutes: 15,
        "tester", new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Multiplies_present_days_by_fixed_daily_yen()
    {
        var inputs = new[]
        {
            new WageInputs(Guid.NewGuid(), 10, 0, 0, 0),
            new WageInputs(Guid.NewGuid(), 0, 0, 0, 0),
        };
        var lines = new FixedWageStrategy().Calculate(inputs, fund: null, Settings(500));
        lines[0].AmountYen.Should().Be(5000);
        lines[1].AmountYen.Should().Be(0);
    }

    [Fact]
    public void Adds_work_and_skill_allowance_on_top_of_fixed_daily_yen()
    {
        var s = WageSettings.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            new DateRange(new DateOnly(2026, 4, 1), null),
            WageMethod.Fixed, RoundingRule.FloorYen, RemainderPolicy.LargestRemainder, 4, fixedDailyYen: 500,
            workAllowancePerDayYen: 300,
            skillAllowanceTiers: new[] { new SkillAllowanceTier(55, 2000) },
            hourUnitMinutes: 15,
            "tester", new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero));
        // 10日×500(固定) + 10*300(作業) + 職能0(60h<55h境界超え) ... 60h>=55h なので職能2000
        var input = new WageInputs(Guid.NewGuid(), PresentDays: 10, TotalWorkedMinutes: 60 * 60, 0, 0);
        var line = new FixedWageStrategy().Calculate(new[] { input }, fund: null, s).Single();
        line.AmountYen.Should().Be(10 * 500 + 10 * 300 + 2_000);
    }

    [Fact]
    public void Throws_if_fixed_daily_yen_missing()
    {
        var s = WageSettings.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            new DateRange(new DateOnly(2026, 4, 1), null),
            WageMethod.Hourly, RoundingRule.FloorYen, RemainderPolicy.LargestRemainder, 4, null,
            workAllowancePerDayYen: null, skillAllowanceTiers: null, hourUnitMinutes: 15,
            "tester", new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero));
        var only = new WageInputs(Guid.NewGuid(), 10, 0, 0, 0);
        FluentActions.Invoking(() => new FixedWageStrategy().Calculate(new[] { only }, null, s))
            .Should().Throw<InvalidOperationException>().WithMessage("*Fixed*");
    }
}
