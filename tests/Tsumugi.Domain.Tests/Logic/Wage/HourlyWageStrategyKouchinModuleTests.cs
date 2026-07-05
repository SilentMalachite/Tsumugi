using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Wage;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests.Logic.Wage;

public class HourlyWageStrategyKouchinModuleTests
{
    private static readonly Guid Office = Guid.Parse("00000000-0000-0000-0000-000000000010");
    private static readonly Guid A = Guid.Parse("00000000-0000-0000-0000-000000000021");

    private static WageSettings Settings(
        int? workAllowancePerDay = 500,
        IReadOnlyList<SkillAllowanceTier>? tiers = null) =>
        WageSettings.Create(
            Guid.NewGuid(), Office,
            new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31)),
            WageMethod.Hourly, RoundingRule.HalfUp, RemainderPolicy.LargestRemainder,
            fiscalYearStartMonth: 4, fixedDailyYen: null,
            workAllowancePerDayYen: workAllowancePerDay,
            skillAllowanceTiers: tiers ?? new[]
            {
                new SkillAllowanceTier(55, 2000),
                new SkillAllowanceTier(70, 4000),
            },
            hourUnitMinutes: 15,
            createdBy: "u", createdAt: DateTimeOffset.UtcNow);

    // KouchinModule v5 突合ケース:
    //   利用日数 15, 就労時間 26h(=1560分), 時給 350 円
    //   → 工賃時給 = ROUND(26 * 350) = 9,100
    //     作業手当 = 15 * 500       = 7,500
    //     職能手当 = 0（26h < 55h）
    //     合計                     = 16,600
    [Fact]
    public void Kouchin_baseline_15days_26h_yen350_totals_16600()
    {
        var inputs = new WageInputs(A, PresentDays: 15, TotalWorkedMinutes: 1560,
            TotalPieceAmountYen: 0, TotalPoints: 0)
        {
            DailyBreakdown = Enumerable.Range(0, 15)
                .Select(i => new DailyHourlyBasis(new DateOnly(2026, 5, i + 1), 104, 350))
                .ToArray(),
        };

        var s = new HourlyWageStrategy();
        var line = s.Calculate(new[] { inputs }, fund: null, settings: Settings()).Single();
        line.AmountYen.Should().Be(16_600);
        line.BasisSummary.Should().Contain("時給").And.Contain("作業").And.Contain("職能");
    }

    [Fact]
    public void Skill_allowance_tier_upper_55_lt_70_returns_2000()
    {
        var inputs = new WageInputs(A, PresentDays: 20, TotalWorkedMinutes: 60 * 60,
            TotalPieceAmountYen: 0, TotalPoints: 0);
        var line = new HourlyWageStrategy()
            .Calculate(new[] { inputs }, fund: null, settings: Settings()).Single();
        // 20 日 * 500 = 10,000（作業）+ 職能 2,000 + 時給 0（DailyBreakdown なしで rate ソースなし）
        line.AmountYen.Should().Be(12_000);
    }

    [Fact]
    public void Skill_allowance_tier_upper_70_returns_4000()
    {
        var inputs = new WageInputs(A, PresentDays: 20, TotalWorkedMinutes: 70 * 60,
            TotalPieceAmountYen: 0, TotalPoints: 0);
        var line = new HourlyWageStrategy()
            .Calculate(new[] { inputs }, fund: null, settings: Settings()).Single();
        line.AmountYen.Should().Be(20 * 500 + 4_000);
    }

    [Fact]
    public void Rejects_minutes_not_multiple_of_hour_unit()
    {
        var inputs = new WageInputs(A, PresentDays: 1, TotalWorkedMinutes: 30,
            TotalPieceAmountYen: 0, TotalPoints: 0)
        {
            DailyBreakdown = new[] { new DailyHourlyBasis(new DateOnly(2026, 5, 1), 7, 350) },
        };
        var act = () => new HourlyWageStrategy()
            .Calculate(new[] { inputs }, fund: null, settings: Settings());
        act.Should().Throw<ArgumentException>().WithMessage("*15分単位*");
    }

    [Fact]
    public void Rejects_negative_total_minutes_in_rate_group()
    {
        var inputs = new WageInputs(A, PresentDays: 1, TotalWorkedMinutes: 0,
            TotalPieceAmountYen: 0, TotalPoints: 0)
        {
            DailyBreakdown = new[]
            {
                new DailyHourlyBasis(new DateOnly(2026, 5, 1), Minutes: -60, HourlyYen: 350),
                new DailyHourlyBasis(new DateOnly(2026, 5, 2), Minutes: 30, HourlyYen: 350),
            },
        };
        var act = () => new HourlyWageStrategy()
            .Calculate(new[] { inputs }, fund: null, settings: Settings());
        act.Should().Throw<ArgumentException>().WithMessage("*負の値*");
    }

    [Fact]
    public void DailyBreakdown_supports_mid_month_rate_change()
    {
        // 前半 10 日 350 円/h × 1h, 後半 5 日 400 円/h × 1h
        var breakdown = Enumerable.Range(0, 10)
            .Select(i => new DailyHourlyBasis(new DateOnly(2026, 5, i + 1), 60, 350))
            .Concat(Enumerable.Range(0, 5)
                .Select(i => new DailyHourlyBasis(new DateOnly(2026, 5, i + 11), 60, 400)))
            .ToArray();
        var inputs = new WageInputs(A, PresentDays: 15, TotalWorkedMinutes: 900,
            TotalPieceAmountYen: 0, TotalPoints: 0) { DailyBreakdown = breakdown };
        var line = new HourlyWageStrategy()
            .Calculate(new[] { inputs }, fund: null, settings: Settings(workAllowancePerDay: 0,
                tiers: Array.Empty<SkillAllowanceTier>())).Single();
        // ROUND(1*350)*10 + ROUND(1*400)*5 = 3500 + 2000 = 5,500
        line.AmountYen.Should().Be(5_500);
    }
}
