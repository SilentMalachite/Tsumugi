using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Domain.Tests;

public sealed class DisabilityCertificateTests
{
    [Fact]
    public void Create_holds_all_fields_and_defaults_nullable_to_null()
    {
        var c = DisabilityCertificate.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            DisabilityCertificateType.Physical, "1級",
            new DateOnly(2020, 4, 1), "東京都",
            "u", DateTimeOffset.UnixEpoch, Guid.NewGuid(),
            subtype: "1種");

        c.Type.Should().Be(DisabilityCertificateType.Physical);
        c.Grade.Should().Be("1級");
        c.Subtype.Should().Be("1種");
        c.IssuingAuthority.Should().Be("東京都");
        c.NextRenewalDate.Should().BeNull();
        c.CertificateNumber.Should().BeNull();
        c.Notes.Should().BeNull();
    }

    [Fact]
    public void Create_rejects_blank_grade()
    {
        var act = () => DisabilityCertificate.Create(
            Guid.NewGuid(), Guid.NewGuid(), DisabilityCertificateType.Mental, " ",
            new DateOnly(2020, 4, 1), "東京都",
            "u", DateTimeOffset.UnixEpoch, Guid.NewGuid());
        act.Should().Throw<ArgumentException>().WithMessage("*等級*");
    }

    [Fact]
    public void Create_rejects_blank_issuing_authority()
    {
        var act = () => DisabilityCertificate.Create(
            Guid.NewGuid(), Guid.NewGuid(), DisabilityCertificateType.Mental, "2級",
            new DateOnly(2020, 4, 1), "",
            "u", DateTimeOffset.UnixEpoch, Guid.NewGuid());
        act.Should().Throw<ArgumentException>().WithMessage("*発行自治体*");
    }

    [Fact]
    public void Create_rejects_renewal_before_issued()
    {
        var act = () => DisabilityCertificate.Create(
            Guid.NewGuid(), Guid.NewGuid(), DisabilityCertificateType.Mental, "2級",
            new DateOnly(2024, 4, 1), "東京都",
            "u", DateTimeOffset.UnixEpoch, Guid.NewGuid(),
            nextRenewalDate: new DateOnly(2024, 1, 1));
        act.Should().Throw<ArgumentException>().WithMessage("*更新予定日*");
    }
}
