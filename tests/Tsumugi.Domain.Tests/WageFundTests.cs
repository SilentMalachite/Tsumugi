using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Logic;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests;

public sealed class WageFundTests
{
    private static readonly Guid Office = Guid.NewGuid();
    private static readonly YearMonth Month = new(2026, 7);
    private static readonly DateTimeOffset T = new(2026, 7, 31, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void NewRecord_is_new_kind()
    {
        var fund = WageFund.NewRecord(Guid.NewGuid(), Office, Month, totalYen: 300000, note: null, "tester", T);
        fund.TotalYen.Should().Be(300000);
        fund.Kind.Should().Be(Domain.Enums.RecordKind.New);
        fund.OriginId.Should().BeNull();
    }

    [Fact]
    public void NewRecord_negative_total_throws()
        => FluentActions.Invoking(() => WageFund.NewRecord(
                Guid.NewGuid(), Office, Month, totalYen: -1, note: null, "t", T))
            .Should().Throw<ArgumentOutOfRangeException>();

    [Fact]
    public void Effective_picks_latest_correction()
    {
        var n = WageFund.NewRecord(Guid.NewGuid(), Office, Month, 300000, null, "t", T);
        var c = WageFund.Correction(Guid.NewGuid(), Office, Month, n.Id, 280000, null, "t", T.AddMinutes(1));
        WageFundPolicy.Effective(new[] { n, c })!.TotalYen.Should().Be(280000);
    }

    [Fact]
    public void Cancellation_yields_null_effective()
    {
        var n = WageFund.NewRecord(Guid.NewGuid(), Office, Month, 300000, null, "t", T);
        var x = WageFund.Cancellation(Guid.NewGuid(), Office, Month, n.Id, "t", T.AddMinutes(1));
        WageFundPolicy.Effective(new[] { n, x }).Should().BeNull();
    }
}
