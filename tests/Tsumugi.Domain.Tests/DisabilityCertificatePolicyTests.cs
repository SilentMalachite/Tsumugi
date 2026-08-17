using System.Globalization;
using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic;
using Xunit;

namespace Tsumugi.Domain.Tests;

public sealed class DisabilityCertificatePolicyTests
{
    private static DisabilityCertificate Cert(
        DisabilityCertificateType type,
        DateOnly? nextRenewalDate,
        DateOnly? issuedDate = null) =>
        DisabilityCertificate.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            type,
            grade: "2級",
            issuedDate: issuedDate ?? new DateOnly(2024, 4, 1),
            issuingAuthority: "東京都",
            createdBy: "u",
            createdAt: DateTimeOffset.UnixEpoch,
            concurrencyToken: Guid.NewGuid(),
            nextRenewalDate: nextRenewalDate);

    [Theory]
    [InlineData("2026-08-01", "2026-08-01", 30, true)]   // 残日数 = 0
    [InlineData("2026-08-01", "2026-08-31", 30, true)]   // 残日数 = しきい値ちょうど
    [InlineData("2026-08-01", "2026-09-01", 30, false)]  // 残日数 = 31（しきい値超）
    [InlineData("2026-08-01", "2026-07-31", 30, false)]  // 過去日（負）
    public void Mental_with_renewal_matches_threshold(
        string asOf, string renewal, int threshold, bool isHit)
    {
        var result = DisabilityCertificatePolicy.FindRenewalDue(
            new[] { Cert(DisabilityCertificateType.Mental, DateOnly.Parse(renewal, CultureInfo.InvariantCulture)) },
            DateOnly.Parse(asOf, CultureInfo.InvariantCulture),
            threshold);

        result.Should().HaveCount(isHit ? 1 : 0);
    }

    [Fact]
    public void Null_renewal_date_is_skipped()
    {
        DisabilityCertificatePolicy.FindRenewalDue(
                new[] { Cert(DisabilityCertificateType.Mental, nextRenewalDate: null) },
                new DateOnly(2026, 8, 1),
                thresholdDays: 30)
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData(DisabilityCertificateType.Physical)]
    [InlineData(DisabilityCertificateType.Intellectual)]
    public void Non_mental_types_are_skipped_even_with_renewal(DisabilityCertificateType type)
    {
        DisabilityCertificatePolicy.FindRenewalDue(
                new[] { Cert(type, new DateOnly(2026, 8, 15)) },
                new DateOnly(2026, 8, 1),
                thresholdDays: 30)
            .Should().BeEmpty();
    }

    [Fact]
    public void Multiple_hits_returned_ordered_by_remaining_ascending()
    {
        var asOf = new DateOnly(2026, 8, 1);
        var day0 = Cert(DisabilityCertificateType.Mental, new DateOnly(2026, 8, 1));
        var day30 = Cert(DisabilityCertificateType.Mental, new DateOnly(2026, 8, 31));
        var day15 = Cert(DisabilityCertificateType.Mental, new DateOnly(2026, 8, 16));
        var outside = Cert(DisabilityCertificateType.Mental, new DateOnly(2026, 9, 1));
        var physical = Cert(DisabilityCertificateType.Physical, new DateOnly(2026, 8, 10));

        var hits = DisabilityCertificatePolicy.FindRenewalDue(
            new[] { day30, outside, physical, day0, day15 },
            asOf,
            thresholdDays: 30);

        hits.Select(x => x.RemainingDays).Should().Equal(0, 15, 30);
        hits.Select(x => x.Certificate).Should().Equal(day0, day15, day30);
    }

    [Fact]
    public void Negative_threshold_throws()
    {
        var act = () => DisabilityCertificatePolicy.FindRenewalDue(
            Array.Empty<DisabilityCertificate>(),
            new DateOnly(2026, 8, 1),
            thresholdDays: -1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
