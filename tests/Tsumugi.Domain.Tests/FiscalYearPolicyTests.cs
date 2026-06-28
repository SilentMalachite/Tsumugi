using FluentAssertions;
using Tsumugi.Domain.Logic;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests;

public sealed class FiscalYearPolicyTests
{
    [Theory]
    [InlineData(2026, 3, 31, 4, 2025)]
    [InlineData(2026, 4, 1, 4, 2026)]
    [InlineData(2027, 1, 31, 4, 2026)]
    [InlineData(2027, 3, 31, 4, 2026)]
    [InlineData(2027, 4, 1, 4, 2027)]
    [InlineData(2026, 12, 31, 1, 2026)]
    public void Fiscal_year_for_calendar_dates(int y, int m, int d, int startMonth, int expected)
        => FiscalYearPolicy.Year(new DateOnly(y, m, d), startMonth).Should().Be(expected);

    [Theory]
    [InlineData(2026, 4, 2026, 4, 2027, 3)]
    [InlineData(2026, 1, 2026, 1, 2026, 12)]
    public void Fiscal_year_start_and_end(int fy, int startMonth, int sy, int sm, int ey, int em)
    {
        FiscalYearPolicy.FiscalYearStart(fy, startMonth).Should().Be(new YearMonth(sy, sm));
        FiscalYearPolicy.FiscalYearEnd(fy, startMonth).Should().Be(new YearMonth(ey, em));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void Invalid_start_month_throws(int startMonth)
        => FluentActions.Invoking(() => FiscalYearPolicy.Year(new DateOnly(2026, 1, 1), startMonth))
            .Should().Throw<ArgumentOutOfRangeException>();
}
