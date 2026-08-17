using FluentAssertions;
using Tsumugi.Domain.Logic;
using Xunit;

namespace Tsumugi.Domain.Tests;

public sealed class FirstRunPolicyTests
{
    // 呼び出し元は SELECT COUNT(*) の結果を渡すので負数は起こらない。
    // 到達しない入力のケースは置かない。
    [Fact]
    public void NeedsFirstRun_is_true_when_no_office_is_registered()
        => FirstRunPolicy.NeedsFirstRun(0).Should().BeTrue();

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(10)]
    public void NeedsFirstRun_is_false_when_office_count_is_one_or_more(int officeCount)
        => FirstRunPolicy.NeedsFirstRun(officeCount).Should().BeFalse();
}
