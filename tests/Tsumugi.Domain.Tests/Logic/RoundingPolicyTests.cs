// tests/Tsumugi.Domain.Tests/Logic/RoundingPolicyTests.cs
using FluentAssertions;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic;
using Xunit;

namespace Tsumugi.Domain.Tests.Logic;

public class RoundingPolicyTests
{
    [Theory]
    [InlineData(100.4, RoundingRule.FloorYen, 100)]
    [InlineData(100.6, RoundingRule.FloorYen, 100)]
    [InlineData(-0.5, RoundingRule.FloorYen, -1)]
    [InlineData(100.4, RoundingRule.HalfUp, 100)]
    [InlineData(100.5, RoundingRule.HalfUp, 101)]
    [InlineData(100.6, RoundingRule.HalfUp, 101)]
    [InlineData(-100.5, RoundingRule.HalfUp, -101)] // AwayFromZero
    [InlineData(100.1, RoundingRule.Ceiling, 101)]
    [InlineData(100.0, RoundingRule.Ceiling, 100)]
    public void Round_returns_expected_integer(decimal amount, RoundingRule rule, int expected)
    {
        RoundingPolicy.Round(amount, rule).Should().Be(expected);
    }

    [Fact]
    public void Round_throws_on_unknown_rule()
    {
        var act = () => RoundingPolicy.Round(100m, (RoundingRule)999);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
