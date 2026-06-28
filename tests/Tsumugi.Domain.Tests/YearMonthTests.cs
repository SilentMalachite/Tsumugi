using FluentAssertions;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests;

public sealed class YearMonthTests
{
    [Theory]
    [InlineData(2026, 7, 2026, 7, 1, 2026, 7, 31)]
    [InlineData(2026, 2, 2026, 2, 1, 2026, 2, 28)]
    [InlineData(2024, 2, 2024, 2, 1, 2024, 2, 29)]
    [InlineData(2026, 12, 2026, 12, 1, 2026, 12, 31)]
    public void First_and_last_day(int y, int m, int fy, int fm, int fd, int ly, int lm, int ld)
    {
        var ym = new YearMonth(y, m);
        ym.FirstDay().Should().Be(new DateOnly(fy, fm, fd));
        ym.LastDay().Should().Be(new DateOnly(ly, lm, ld));
    }

    [Theory]
    [InlineData(2026, 12, 2027, 1)]
    [InlineData(2026, 1, 2026, 2)]
    public void Next_wraps_year(int y, int m, int ey, int em)
        => new YearMonth(y, m).Next().Should().Be(new YearMonth(ey, em));

    [Theory]
    [InlineData(2026, 1, 2025, 12)]
    public void Previous_wraps_year(int y, int m, int ey, int em)
        => new YearMonth(y, m).Previous().Should().Be(new YearMonth(ey, em));

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1899, 12)]
    [InlineData(2201, 1)]
    [InlineData(2026, 0)]
    [InlineData(2026, 13)]
    public void Out_of_range_throws(int y, int m)
        => FluentActions.Invoking(() => new YearMonth(y, m))
            .Should().Throw<ArgumentOutOfRangeException>();

    [Fact]
    public void FromDate_returns_year_and_month()
        => YearMonth.FromDate(new DateOnly(2026, 7, 15))
            .Should().Be(new YearMonth(2026, 7));

    [Fact]
    public void Comparable_ordering()
    {
        var a = new YearMonth(2026, 6);
        var b = new YearMonth(2026, 7);
        a.CompareTo(b).Should().BeNegative();
        b.CompareTo(a).Should().BePositive();
        a.CompareTo(a).Should().Be(0);
    }
}
