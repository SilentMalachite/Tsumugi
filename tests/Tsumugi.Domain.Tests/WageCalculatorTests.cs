using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic;
using Tsumugi.Domain.Logic.Wage;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests;

public sealed class WageCalculatorTests
{
    private static readonly Guid Office = Guid.NewGuid();
    private static readonly DateTimeOffset T = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly IReadOnlyList<IWageMethodStrategy> AllStrategies = new IWageMethodStrategy[]
    {
        new PieceWageStrategy(), new HourlyWageStrategy(),
        new FixedWageStrategy(), new EqualWageStrategy(),
    };

    private static WageSettings Settings(WageMethod m, int? fixedYen = null) => WageSettings.Create(
        Guid.NewGuid(), Office, new DateRange(new DateOnly(2026, 4, 1), null),
        m, RoundingRule.FloorYen, RemainderPolicy.LargestRemainder, 4, fixedYen, "t", T);

    [Fact]
    public void Selects_strategy_matching_settings_method()
    {
        var inputs = new[] { new WageInputs(Guid.NewGuid(), 10, 600, 0, 0) };
        var fund = WageFund.NewRecord(Guid.NewGuid(), Office, new YearMonth(2026, 7), 50_000, null, "t", T);
        var lines = WageCalculator.Calculate(AllStrategies, WageMethod.Hourly, inputs, fund, Settings(WageMethod.Hourly));
        lines.Should().HaveCount(1);
        lines[0].AmountYen.Should().Be(50_000);
    }

    [Fact]
    public void Throws_if_strategy_for_method_not_registered()
    {
        var inputs = new[] { new WageInputs(Guid.NewGuid(), 10, 600, 0, 0) };
        var onlyPiece = new IWageMethodStrategy[] { new PieceWageStrategy() };
        FluentActions.Invoking(() => WageCalculator.Calculate(
                onlyPiece, WageMethod.Hourly, inputs, null, Settings(WageMethod.Hourly)))
            .Should().Throw<InvalidOperationException>().WithMessage("*Hourly*");
    }
}
