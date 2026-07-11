using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Logic.Claim;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests.Logic.Claim;

public sealed class CertificatePolicyTests
{
    private static readonly DateOnly ServiceDate = new(2026, 7, 1);

    [Fact]
    public void EffectiveVersion_returns_latest_revision_in_effective_chain()
    {
        var root = Root();
        var correction = Correction(root);

        var result = CertificatePolicy.EffectiveVersion([root, correction], ServiceDate);

        result.Should().Be(correction);
    }

    [Fact]
    public void EffectiveVersion_returns_null_when_no_root_is_effective()
    {
        var root = Root(new DateRange(new DateOnly(2025, 4, 1), new DateOnly(2026, 3, 31)));

        var result = CertificatePolicy.EffectiveVersion([root], ServiceDate);

        result.Should().BeNull();
    }

    [Fact]
    public void EffectiveVersion_rejects_branching_heads()
    {
        var root = Root();
        var a = Correction(root);
        var b = Correction(root);

        var act = () => CertificatePolicy.EffectiveVersion([root, a, b], ServiceDate);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void EffectiveVersion_rejects_missing_revision()
    {
        var root = Root();
        var correction = Correction(root) with { Revision = 3 };

        var act = () => CertificatePolicy.EffectiveVersion([root, correction], ServiceDate);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void EffectiveVersion_rejects_expected_head_that_is_not_previous_revision()
    {
        var root = Root();
        var correction = Correction(root) with { ExpectedHeadCertificateId = Guid.NewGuid() };

        var act = () => CertificatePolicy.EffectiveVersion([root, correction], ServiceDate);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void EffectiveVersion_rejects_root_with_lineage_parent()
    {
        var root = Root() with { ExpectedHeadCertificateId = Guid.NewGuid() };

        var act = () => CertificatePolicy.EffectiveVersion([root], ServiceDate);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void EffectiveVersion_rejects_multiple_effective_roots()
    {
        var first = Root();
        var second = Root();

        var act = () => CertificatePolicy.EffectiveVersion([first, second], ServiceDate);

        act.Should().Throw<InvalidOperationException>();
    }

    private static Certificate Root(DateRange? validity = null)
    {
        var id = Guid.NewGuid();
        return Certificate.Create(
            id,
            Guid.NewGuid(),
            "1234567890",
            validity ?? new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31)),
            supplyDays: 23,
            monthlyCostCap: 9_300,
            municipality: "杉並区",
            createdBy: "tester",
            createdAt: DateTimeOffset.UnixEpoch,
            concurrencyToken: Guid.NewGuid());
    }

    private static Certificate Correction(Certificate head) => head with
    {
        Id = Guid.NewGuid(),
        RootCertificateId = head.RootCertificateId,
        Revision = head.Revision + 1,
        ExpectedHeadCertificateId = head.Id,
        CreatedAt = head.CreatedAt.AddMinutes(1),
        ConcurrencyToken = Guid.NewGuid(),
    };
}
