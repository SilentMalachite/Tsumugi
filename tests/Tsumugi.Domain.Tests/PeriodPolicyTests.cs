using FluentAssertions;
using Tsumugi.Domain.Logic;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests;

public sealed class PeriodPolicyTests
{
    [Fact]
    public void DetectOverlaps_finds_pairs()
    {
        var ranges = new[]
        {
            new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2026, 6, 30)),
            new DateRange(new DateOnly(2026, 6, 1), new DateOnly(2026, 8, 31)),  // overlaps [0]
            new DateRange(new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 31)),
        };
        PeriodPolicy.DetectOverlaps(ranges).Should().ContainSingle().Which.Should().Be((0, 1));
    }

    [Fact]
    public void DetectGaps_finds_non_contiguous_ranges()
    {
        var ranges = new[]
        {
            new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2026, 6, 30)),
            new DateRange(new DateOnly(2026, 8, 1), new DateOnly(2026, 12, 31)),  // gap 7月
        };
        var gaps = PeriodPolicy.DetectGaps(ranges);
        gaps.Should().ContainSingle();
        gaps[0].Start.Should().Be(new DateOnly(2026, 7, 1));
        gaps[0].End.Should().Be(new DateOnly(2026, 7, 31));
    }
}
