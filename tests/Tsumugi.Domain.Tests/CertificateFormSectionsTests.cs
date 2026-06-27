using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Tests;

public sealed class CertificateFormSectionsTests
{
    private static DateRange Validity =>
        new(new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31));

    [Fact]
    public void Create_defaults_match_MHLW_form_safe_values()
    {
        var c = Certificate.Create(
            Guid.NewGuid(), Guid.NewGuid(), "0000000000",
            Validity, 23, 0, "杉並区",
            "u", DateTimeOffset.UnixEpoch, Guid.NewGuid());

        c.RecipientGender.Should().Be(Gender.Unspecified);
        c.SupportCategory.Should().Be(SupportCategory.None);
        c.BenefitType.Should().Be(BenefitType.Training, "Tsumugi の主対象は訓練等給付");
        c.ServiceCategory.Should().Be("就労継続支援B型");
        c.PaymentBurden.Should().Be(PaymentBurdenCategory.Unspecified);
        c.Disabilities.Any.Should().BeFalse();
        c.MealProvisionApplicable.Should().BeFalse();
    }

    [Fact]
    public void Create_persists_all_optional_sections()
    {
        var c = Certificate.Create(
            Guid.NewGuid(), Guid.NewGuid(), "1234567890",
            Validity, 23, 9300, "杉並区",
            "u", DateTimeOffset.UnixEpoch, Guid.NewGuid(),
            recipientAddress: "東京都杉並区...",
            recipientGender: Gender.Male,
            guardianName: "保護者",
            guardianRelationship: "父",
            disabilities: new DisabilityCategories(true, false, true, false),
            supportCategory: SupportCategory.Category5,
            paymentBurden: PaymentBurdenCategory.Welfare,
            mealProvisionApplicable: true);

        c.RecipientAddress.Should().Be("東京都杉並区...");
        c.RecipientGender.Should().Be(Gender.Male);
        c.GuardianRelationship.Should().Be("父");
        c.Disabilities.Physical.Should().BeTrue();
        c.Disabilities.Mental.Should().BeTrue();
        c.Disabilities.Intellectual.Should().BeFalse();
        c.SupportCategory.Should().Be(SupportCategory.Category5);
        c.PaymentBurden.Should().Be(PaymentBurdenCategory.Welfare);
        c.MealProvisionApplicable.Should().BeTrue();
    }
}

public sealed class ContractedProviderTests
{
    [Fact]
    public void Create_holds_provider_section_fields()
    {
        var certId = Guid.NewGuid();
        var p = ContractedProvider.Create(
            Guid.NewGuid(), certId, "1010101010", "Tsumugi 作業所",
            "就労継続支援B型", 23, new DateOnly(2026, 4, 1),
            "u", DateTimeOffset.UnixEpoch, Guid.NewGuid(),
            terminationDate: new DateOnly(2026, 12, 31), notes: "備考");

        p.CertificateId.Should().Be(certId);
        p.ProviderName.Should().Be("Tsumugi 作業所");
        p.ContractedSupplyDays.Should().Be(23);
        p.TerminationDate.Should().Be(new DateOnly(2026, 12, 31));
        p.Notes.Should().Be("備考");
    }

    [Fact]
    public void Create_rejects_negative_supply_days()
    {
        var act = () => ContractedProvider.Create(
            Guid.NewGuid(), Guid.NewGuid(), "x", "y", "z",
            -1, new DateOnly(2026, 4, 1),
            "u", DateTimeOffset.UnixEpoch, Guid.NewGuid());

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
