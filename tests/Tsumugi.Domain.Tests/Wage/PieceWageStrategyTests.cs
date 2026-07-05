using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Wage;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests.Wage;

public sealed class PieceWageStrategyTests
{
    private static WageSettings Settings() => WageSettings.Create(
        Guid.NewGuid(), Guid.NewGuid(),
        new DateRange(new DateOnly(2026, 4, 1), null),
        WageMethod.Piece, RoundingRule.FloorYen, RemainderPolicy.LargestRemainder,
        fiscalYearStartMonth: 4, fixedDailyYen: null,
        workAllowancePerDayYen: null, skillAllowanceTiers: null, hourUnitMinutes: 15,
        "tester", new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Calculates_per_recipient_from_piece_amount()
    {
        var inputs = new[]
        {
            new WageInputs(Guid.NewGuid(), PresentDays: 10, TotalWorkedMinutes: 1200, TotalPieceAmountYen: 5_400, TotalPoints: 0),
            new WageInputs(Guid.NewGuid(), PresentDays: 8, TotalWorkedMinutes: 960, TotalPieceAmountYen: 3_120, TotalPoints: 0),
        };
        var lines = new PieceWageStrategy().Calculate(inputs, fund: null, Settings());
        lines.Should().HaveCount(2);
        lines[0].AmountYen.Should().Be(5_400);
        lines[1].AmountYen.Should().Be(3_120);
        lines.Sum(l => l.AmountYen).Should().Be(8_520);
    }
}
