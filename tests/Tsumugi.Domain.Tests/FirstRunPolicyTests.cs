using FluentAssertions;
using Tsumugi.Domain.Logic;
using Xunit;

namespace Tsumugi.Domain.Tests;

public sealed class FirstRunPolicyTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NeedsFirstRun_is_true_when_office_count_is_below_one(int officeCount)
        => FirstRunPolicy.NeedsFirstRun(officeCount).Should().BeTrue();

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(10)]
    public void NeedsFirstRun_is_false_when_office_count_is_one_or_more(int officeCount)
        => FirstRunPolicy.NeedsFirstRun(officeCount).Should().BeFalse();
}
