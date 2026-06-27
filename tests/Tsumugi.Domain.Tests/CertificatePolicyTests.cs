using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Logic;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests;

public sealed class CertificatePolicyTests
{
    private static Certificate Cert(DateOnly end) => Certificate.Create(
        Guid.NewGuid(), Guid.NewGuid(), "n",
        new DateRange(new DateOnly(2026, 1, 1), end),
        supplyDays: 0, monthlyCostCap: 0, municipality: "x",
        "u", DateTimeOffset.UnixEpoch, Guid.NewGuid());

    [Theory]
    [InlineData("2026-06-27", "2026-07-27", 30, true)]   // 残日数 = しきい値ちょうど
    [InlineData("2026-06-27", "2026-06-27", 30, true)]   // 残日数 = 0
    [InlineData("2026-06-27", "2026-06-26", 30, false)]  // 失効（負）
    [InlineData("2026-06-27", "2026-07-28", 30, false)]  // しきい値より遠い
    public void Single_certificate_matches_threshold(string asOf, string end, int threshold, bool isHit)
    {
        var result = CertificatePolicy.FindExpiring(
            new[] { Cert(DateOnly.Parse(end)) },
            DateOnly.Parse(asOf),
            threshold);
        result.Should().HaveCount(isHit ? 1 : 0);
    }

    [Fact]
    public void Empty_list_returns_empty()
    {
        CertificatePolicy.FindExpiring(Array.Empty<Certificate>(), new DateOnly(2026, 6, 27), 30)
            .Should().BeEmpty();
    }

    [Fact]
    public void Multiple_certificates_returned_ordered_by_remaining_ascending()
    {
        var asOf = new DateOnly(2026, 6, 27);
        var near = Cert(new DateOnly(2026, 6, 30));   // 残3日
        var far = Cert(new DateOnly(2026, 7, 25));    // 残28日
        var result = CertificatePolicy.FindExpiring(new[] { far, near }, asOf, thresholdDays: 30);
        result.Should().HaveCount(2);
        result[0].RemainingDays.Should().Be(3);
        result[1].RemainingDays.Should().Be(28);
    }

    [Fact]
    public void Open_ended_certificate_is_skipped()
    {
        var open = Certificate.Create(Guid.NewGuid(), Guid.NewGuid(), "n",
            new DateRange(new DateOnly(2026, 1, 1), End: null),
            0, 0, "x", "u", DateTimeOffset.UnixEpoch, Guid.NewGuid());
        CertificatePolicy.FindExpiring(new[] { open }, new DateOnly(2026, 6, 27), 30)
            .Should().BeEmpty();
    }
}
