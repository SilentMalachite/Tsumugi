using FluentAssertions;
using Tsumugi.App.Formatting;
using Xunit;

namespace Tsumugi.App.Tests;

public sealed class YenFormatterTests
{
    [Theory]
    [InlineData(0, "0 円")]
    [InlineData(1, "1 円")]
    [InlineData(1000, "1,000 円")]
    [InlineData(1_234_567, "1,234,567 円")]
    public void Format_integer_yen_with_separator(int yen, string expected)
        => YenFormatter.Format(yen).Should().Be(expected);

    [Theory]
    [InlineData(-1, "-1 円")]
    [InlineData(-1_234, "-1,234 円")]
    public void Format_negative_yen(int yen, string expected)
        => YenFormatter.Format(yen).Should().Be(expected);
}
