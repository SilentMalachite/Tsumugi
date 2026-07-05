using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests;

public class WageSettingsAllowanceExtensionTests
{
    private static readonly Guid OfficeId = Guid.Parse("00000000-0000-0000-0000-000000000010");
    private static readonly DateRange Period =
        new(new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31));

    [Fact]
    public void Create_defaults_hour_unit_to_15_and_empty_tiers()
    {
        var s = WageSettings.Create(
            Guid.NewGuid(), OfficeId, Period,
            WageMethod.Hourly, RoundingRule.HalfUp, RemainderPolicy.LargestRemainder,
            fiscalYearStartMonth: 4, fixedDailyYen: null,
            workAllowancePerDayYen: 500,
            skillAllowanceTiers: null,
            hourUnitMinutes: 15,
            createdBy: "tester", createdAt: DateTimeOffset.UtcNow);

        s.WorkAllowancePerDayYen.Should().Be(500);
        s.SkillAllowanceTiers.Should().BeEmpty();
        s.HourUnitMinutes.Should().Be(15);
    }

    [Fact]
    public void Create_accepts_sorted_tier_list()
    {
        var tiers = new[] { new SkillAllowanceTier(55, 2000), new SkillAllowanceTier(70, 4000) };
        var s = WageSettings.Create(
            Guid.NewGuid(), OfficeId, Period,
            WageMethod.Hourly, RoundingRule.HalfUp, RemainderPolicy.LargestRemainder,
            4, null, 500, tiers, 15, "tester", DateTimeOffset.UtcNow);
        s.SkillAllowanceTiers.Should().Equal(tiers);
    }

    [Fact]
    public void Create_rejects_unsorted_tiers()
    {
        var tiers = new[] { new SkillAllowanceTier(70, 4000), new SkillAllowanceTier(55, 2000) };
        var act = () => WageSettings.Create(
            Guid.NewGuid(), OfficeId, Period,
            WageMethod.Hourly, RoundingRule.HalfUp, RemainderPolicy.LargestRemainder,
            4, null, 500, tiers, 15, "tester", DateTimeOffset.UtcNow);
        act.Should().Throw<ArgumentException>().WithMessage("*昇順*");
    }

    [Fact]
    public void Create_rejects_duplicate_thresholds()
    {
        var tiers = new[] { new SkillAllowanceTier(55, 2000), new SkillAllowanceTier(55, 3000) };
        var act = () => WageSettings.Create(
            Guid.NewGuid(), OfficeId, Period,
            WageMethod.Hourly, RoundingRule.HalfUp, RemainderPolicy.LargestRemainder,
            4, null, 500, tiers, 15, "tester", DateTimeOffset.UtcNow);
        act.Should().Throw<ArgumentException>().WithMessage("*重複*");
    }

    [Fact]
    public void Create_rejects_negative_amounts()
    {
        var act1 = () => WageSettings.Create(
            Guid.NewGuid(), OfficeId, Period,
            WageMethod.Hourly, RoundingRule.HalfUp, RemainderPolicy.LargestRemainder,
            4, null, -1, null, 15, "tester", DateTimeOffset.UtcNow);
        act1.Should().Throw<ArgumentOutOfRangeException>();

        var tiers = new[] { new SkillAllowanceTier(55, -10) };
        var act2 = () => WageSettings.Create(
            Guid.NewGuid(), OfficeId, Period,
            WageMethod.Hourly, RoundingRule.HalfUp, RemainderPolicy.LargestRemainder,
            4, null, 500, tiers, 15, "tester", DateTimeOffset.UtcNow);
        act2.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]  // 60 の約数ではない
    [InlineData(61)]
    public void Create_rejects_invalid_hour_unit(int minutes)
    {
        var act = () => WageSettings.Create(
            Guid.NewGuid(), OfficeId, Period,
            WageMethod.Hourly, RoundingRule.HalfUp, RemainderPolicy.LargestRemainder,
            4, null, 500, null, minutes, "tester", DateTimeOffset.UtcNow);
        act.Should().Throw<ArgumentException>().WithMessage("*分単位*");
    }
}

public sealed class WageSettingsTests
{
    private static readonly Guid Office = Guid.NewGuid();
    private static readonly DateTimeOffset T = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    private static WageSettings Settings(DateRange period, WageMethod method, int? fixedYen = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            OfficeId = Office,
            Period = period,
            Method = method,
            Rounding = RoundingRule.FloorYen,
            Remainder = RemainderPolicy.LargestRemainder,
            FiscalYearStartMonth = 4,
            FixedDailyYen = fixedYen,
            SkillAllowanceTiers = Array.Empty<SkillAllowanceTier>(),
            HourUnitMinutes = 15,
            CreatedAt = T,
            CreatedBy = "tester",
            ConcurrencyToken = Guid.NewGuid(),
        };

    [Fact]
    public void Effective_returns_settings_whose_period_contains_asOf()
    {
        var s1 = Settings(new DateRange(new DateOnly(2025, 4, 1), new DateOnly(2026, 3, 31)), WageMethod.Equal);
        var s2 = Settings(new DateRange(new DateOnly(2026, 4, 1), null), WageMethod.Hourly);
        WageSettingsPolicy.Effective(new[] { s1, s2 }, new DateOnly(2026, 7, 1)).Should().Be(s2);
        WageSettingsPolicy.Effective(new[] { s1, s2 }, new DateOnly(2025, 5, 1)).Should().Be(s1);
    }

    [Fact]
    public void Effective_returns_null_when_no_period_contains_asOf()
    {
        var s1 = Settings(new DateRange(new DateOnly(2025, 4, 1), new DateOnly(2026, 3, 31)), WageMethod.Equal);
        WageSettingsPolicy.Effective(new[] { s1 }, new DateOnly(2027, 1, 1)).Should().BeNull();
    }

    [Fact]
    public void Fixed_method_without_fixed_daily_yen_throws()
    {
        FluentActions.Invoking(() => WageSettings.Create(
                Guid.NewGuid(), Office,
                new DateRange(new DateOnly(2026, 4, 1), null),
                WageMethod.Fixed, RoundingRule.FloorYen,
                RemainderPolicy.LargestRemainder, 4, null, "tester", T))
            .Should().Throw<ArgumentException>()
            .WithMessage("*FixedDailyYen*");
    }
}
