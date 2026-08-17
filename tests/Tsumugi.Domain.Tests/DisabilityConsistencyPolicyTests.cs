using FluentAssertions;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests;

public sealed class DisabilityConsistencyPolicyTests
{
    [Fact]
    public void All_three_types_matching_returns_empty()
    {
        var disabilities = new DisabilityCategories(
            Physical: true, Intellectual: true, Mental: true, Intractable: false);
        var types = new HashSet<DisabilityCertificateType>
        {
            DisabilityCertificateType.Physical,
            DisabilityCertificateType.Intellectual,
            DisabilityCertificateType.Mental,
        };

        DisabilityConsistencyPolicy.Detect(disabilities, types).Should().BeEmpty();
    }

    [Fact]
    public void Certificate_only_physical_is_certificate_only()
    {
        var disabilities = new DisabilityCategories(
            Physical: true, Intellectual: false, Mental: false, Intractable: false);

        var findings = DisabilityConsistencyPolicy.Detect(
            disabilities, new HashSet<DisabilityCertificateType>());

        findings.Should().ContainSingle();
        findings[0].Type.Should().Be(DisabilityCertificateType.Physical);
        findings[0].Direction.Should().Be(DisabilityConsistencyDirection.CertificateOnly);
    }

    [Fact]
    public void Handbook_only_mental_is_handbook_only()
    {
        var disabilities = DisabilityCategories.None;
        var types = new HashSet<DisabilityCertificateType> { DisabilityCertificateType.Mental };

        var findings = DisabilityConsistencyPolicy.Detect(disabilities, types);

        findings.Should().ContainSingle();
        findings[0].Type.Should().Be(DisabilityCertificateType.Mental);
        findings[0].Direction.Should().Be(DisabilityConsistencyDirection.HandbookOnly);
    }

    [Fact]
    public void Bidirectional_findings_follow_physical_intellectual_mental_order()
    {
        var disabilities = new DisabilityCategories(
            Physical: true, Intellectual: false, Mental: true, Intractable: false);
        var types = new HashSet<DisabilityCertificateType>
        {
            DisabilityCertificateType.Intellectual,
            DisabilityCertificateType.Mental,
        };

        var findings = DisabilityConsistencyPolicy.Detect(disabilities, types);

        findings.Should().HaveCount(2);
        findings[0].Should().Be(new DisabilityConsistencyFinding(
            DisabilityCertificateType.Physical, DisabilityConsistencyDirection.CertificateOnly));
        findings[1].Should().Be(new DisabilityConsistencyFinding(
            DisabilityCertificateType.Intellectual, DisabilityConsistencyDirection.HandbookOnly));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Intractable_is_never_detected(bool intractable)
    {
        var disabilities = new DisabilityCategories(
            Physical: false, Intellectual: false, Mental: false, Intractable: intractable);

        DisabilityConsistencyPolicy.Detect(
                disabilities, new HashSet<DisabilityCertificateType>())
            .Should().BeEmpty();
    }
}
