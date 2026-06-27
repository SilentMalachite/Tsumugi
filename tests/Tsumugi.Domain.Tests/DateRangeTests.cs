using FluentAssertions;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests;

public sealed class DateRangeTests
{
    [Theory]
    [InlineData("2026-04-01", "2026-04-30", "2026-04-01", true)]   // 開始日ちょうど
    [InlineData("2026-04-01", "2026-04-30", "2026-04-30", true)]   // 終了日ちょうど（両端含む）
    [InlineData("2026-04-01", "2026-04-30", "2026-04-15", true)]
    [InlineData("2026-04-01", "2026-04-30", "2026-03-31", false)]
    [InlineData("2026-04-01", "2026-04-30", "2026-05-01", false)]
    public void Contains_handles_both_ends_inclusive(string s, string e, string d, bool expected)
    {
        var range = new DateRange(DateOnly.Parse(s), DateOnly.Parse(e));
        range.Contains(DateOnly.Parse(d)).Should().Be(expected);
    }

    [Fact]
    public void Contains_with_open_end_is_unbounded()
    {
        var range = new DateRange(new DateOnly(2026, 4, 1), End: null);
        range.Contains(new DateOnly(2099, 12, 31)).Should().BeTrue();
        range.Contains(new DateOnly(2026, 3, 31)).Should().BeFalse();
    }

    [Fact]
    public void Construction_rejects_inverted_range()
    {
#pragma warning disable CA1806 // void lambda discards constructed struct intentionally
        Action act = () => new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2026, 3, 31));
#pragma warning restore CA1806
        act.Should().Throw<ArgumentException>();
    }
}
