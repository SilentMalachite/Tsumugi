using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests;

public sealed class WageStatementTests
{
    [Fact]
    public void NewRecord_is_new_kind()
    {
        var s = WageStatement.NewRecord(
            Guid.NewGuid(), Guid.NewGuid(), new YearMonth(2026, 7), Guid.NewGuid(),
            amountYen: 12_345, basisSummary: "時間割: 600分", createdBy: "tester",
            createdAt: new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero));
        s.Kind.Should().Be(RecordKind.New);
        s.OriginId.Should().BeNull();
        s.AmountYen.Should().Be(12_345);
    }

    [Fact]
    public void Negative_amount_throws()
    {
        FluentActions.Invoking(() => WageStatement.NewRecord(
            Guid.NewGuid(), Guid.NewGuid(), new YearMonth(2026, 7), Guid.NewGuid(),
            -1, "x", "t", DateTimeOffset.UtcNow))
            .Should().Throw<ArgumentOutOfRangeException>();
    }
}
