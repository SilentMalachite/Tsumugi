using FluentAssertions;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests.ValueObjects;

public class SkillAllowanceTierTests
{
    [Fact]
    public void Tier_holds_min_hours_and_yen()
    {
        var t = new SkillAllowanceTier(55, 2000);
        t.MinHours.Should().Be(55);
        t.Yen.Should().Be(2000);
    }

    // 妥当性チェックはコレクション単位で WageSettings.Create が担うため、
    // ここでは負値も型的に許容する（プリミティブ record として）。
    [Fact]
    public void Tier_allows_zero_thresholds_and_zero_yen()
    {
        var t = new SkillAllowanceTier(0, 0);
        t.MinHours.Should().Be(0);
        t.Yen.Should().Be(0);
    }
}
