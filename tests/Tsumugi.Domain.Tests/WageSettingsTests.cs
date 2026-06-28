using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests;

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
