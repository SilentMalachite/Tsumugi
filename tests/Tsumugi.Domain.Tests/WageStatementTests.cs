using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests;

public sealed class WageStatementTests
{
    private static readonly Guid Office = Guid.NewGuid();
    private static readonly YearMonth Month = new YearMonth(2026, 7);
    private static readonly Guid R1 = Guid.NewGuid();
    private static readonly DateTimeOffset T = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);

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
    public void NewRecord_throws_when_amount_is_negative()
    {
        var act = () => WageStatement.NewRecord(
            Guid.NewGuid(), Office, Month, R1, amountYen: -1, basisSummary: "x", "t", T);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("amountYen");
    }

    [Fact]
    public void NewRecord_throws_when_basis_summary_is_empty()
    {
        var act = () => WageStatement.NewRecord(
            Guid.NewGuid(), Office, Month, R1, amountYen: 0, basisSummary: "", "t", T);
        act.Should().Throw<ArgumentException>().WithParameterName("basisSummary");
    }

    [Fact]
    public void Correction_throws_when_amount_is_negative()
    {
        var act = () => WageStatement.Correction(
            Guid.NewGuid(), Office, Month, R1, originId: Guid.NewGuid(),
            amountYen: -1, basisSummary: "x", "t", T);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("amountYen");
    }

    [Fact]
    public void Correction_throws_when_basis_summary_is_empty()
    {
        var act = () => WageStatement.Correction(
            Guid.NewGuid(), Office, Month, R1, originId: Guid.NewGuid(),
            amountYen: 100, basisSummary: "", "t", T);
        act.Should().Throw<ArgumentException>().WithParameterName("basisSummary");
    }
}
