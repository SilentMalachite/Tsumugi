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
        WageMethod.Equal, RoundingRule.FloorYen, RemainderPolicy.LargestRemainder, 4, null, "t", T);

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
}
