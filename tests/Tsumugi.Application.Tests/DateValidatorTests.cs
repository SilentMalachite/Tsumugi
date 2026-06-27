using FluentAssertions;
using Tsumugi.Application.Validation;
using Xunit;

namespace Tsumugi.Application.Tests;

public sealed class DateValidatorTests
{
    [Fact]
    public void EnsureValid_passes_for_realistic_date()
    {
        var act = () => DateValidator.EnsureValid(new DateOnly(2026, 6, 27), "x");
        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureValid_rejects_min_value()
    {
        var act = () => DateValidator.EnsureValid(DateOnly.MinValue, "誕生日");
        act.Should().Throw<DateValidationException>().Where(e => e.FieldName == "誕生日");
    }

    [Fact]
    public void EnsureRange_rejects_inverted_range()
    {
        var act = () => DateValidator.EnsureRange(
            new DateOnly(2026, 6, 1), new DateOnly(2026, 5, 31), "有効期間");
        act.Should().Throw<DateValidationException>();
    }

    [Fact]
    public void EnsureRange_allows_open_end()
    {
        var act = () => DateValidator.EnsureRange(new DateOnly(2026, 4, 1), null, "契約期間");
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(2026, 0)]
    [InlineData(2026, 13)]
    [InlineData(1899, 6)]
    public void EnsureYearMonth_rejects_out_of_range(int y, int m)
    {
        var act = () => DateValidator.EnsureYearMonth(y, m);
        act.Should().Throw<DateValidationException>();
    }
}
