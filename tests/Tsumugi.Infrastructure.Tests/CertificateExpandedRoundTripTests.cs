using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;
using Tsumugi.Infrastructure.Persistence;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

/// <summary>
/// 拡張された Certificate（MHLW 様式準拠の追加列）と ContractedProvider 子エンティティが
/// 実 SQLite に往復することを確認する。マイグレーション ExpandCertificateAndContractedProvider の検証。
/// </summary>
public sealed class CertificateExpandedRoundTripTests : IClassFixture<SqliteFixture>
{
    private readonly SqliteFixture _fixture;
    public CertificateExpandedRoundTripTests(SqliteFixture f) => _fixture = f;

    [Fact]
    public async Task Certificate_with_all_form_sections_round_trips()
    {
        var id = Guid.NewGuid();
        await using (var ctx = _fixture.NewContext())
        {
            var c = Certificate.Create(
                id, Guid.NewGuid(), "7777777777",
                new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31)),
                23, 9300, "杉並区", "u", DateTimeOffset.UnixEpoch, Guid.NewGuid(),
                recipientAddress: "東京都杉並区...",
                recipientGender: Gender.Female,
                guardianName: "保護者",
                guardianRelationship: "母",
                disabilities: new DisabilityCategories(true, true, false, true),
                supportCategory: SupportCategory.Category4,
                benefitType: BenefitType.Training,
                serviceCategory: "就労継続支援B型",
                supplyNotes: "週3日",
                consultationProviderName: "相談センターA",
                consultationProviderNumber: "9999999999",
                consultationStart: new DateOnly(2026, 4, 1),
                consultationEnd: new DateOnly(2027, 3, 31),
                paymentBurden: PaymentBurdenCategory.General1,
                upperLimitManagementProvider: "事業所A",
                mealProvisionApplicable: true,
                highCostBenefitApplicable: true);
            ctx.Certificates.Add(c);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = _fixture.NewContext())
        {
            var loaded = await ctx.Certificates.AsNoTracking().FirstAsync(x => x.Id == id);
            loaded.RecipientAddress.Should().Be("東京都杉並区...");
            loaded.RecipientGender.Should().Be(Gender.Female);
            loaded.GuardianName.Should().Be("保護者");
            loaded.GuardianRelationship.Should().Be("母");
            loaded.Disabilities.Physical.Should().BeTrue();
            loaded.Disabilities.Intellectual.Should().BeTrue();
            loaded.Disabilities.Mental.Should().BeFalse();
            loaded.Disabilities.Intractable.Should().BeTrue();
            loaded.SupportCategory.Should().Be(SupportCategory.Category4);
            loaded.BenefitType.Should().Be(BenefitType.Training);
            loaded.ServiceCategory.Should().Be("就労継続支援B型");
            loaded.SupplyNotes.Should().Be("週3日");
            loaded.ConsultationProviderName.Should().Be("相談センターA");
            loaded.ConsultationProviderNumber.Should().Be("9999999999");
            loaded.ConsultationStart.Should().Be(new DateOnly(2026, 4, 1));
            loaded.ConsultationEnd.Should().Be(new DateOnly(2027, 3, 31));
            loaded.PaymentBurden.Should().Be(PaymentBurdenCategory.General1);
            loaded.UpperLimitManagementProvider.Should().Be("事業所A");
            loaded.MealProvisionApplicable.Should().BeTrue();
            loaded.HighCostBenefitApplicable.Should().BeTrue();
        }
    }

    [Fact]
    public async Task ContractedProvider_round_trips_and_lists_by_certificate()
    {
        var certId = Guid.NewGuid();
        await using (var ctx = _fixture.NewContext())
        {
            // Certificate 親レコードを先に挿入（外部キー制約は無いが意図整合のため）
            ctx.Certificates.Add(Certificate.Create(
                certId, Guid.NewGuid(), "8888888888",
                new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31)),
                23, 0, "杉並区", "u", DateTimeOffset.UnixEpoch, Guid.NewGuid()));
            ctx.ContractedProviders.Add(ContractedProvider.Create(
                Guid.NewGuid(), certId, "1010101010", "Tsumugi 作業所",
                "就労継続支援B型", 23, new DateOnly(2026, 4, 1),
                "u", DateTimeOffset.UnixEpoch, Guid.NewGuid(),
                notes: "備考"));
            ctx.ContractedProviders.Add(ContractedProvider.Create(
                Guid.NewGuid(), certId, "2020202020", "別事業所",
                "生活介護", 5, new DateOnly(2026, 5, 1),
                "u", DateTimeOffset.UnixEpoch, Guid.NewGuid(),
                terminationDate: new DateOnly(2026, 12, 31)));
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = _fixture.NewContext())
        {
            var repo = new ContractedProviderRepository(ctx);
            var list = await repo.ListByCertificateAsync(certId, default);
            list.Should().HaveCount(2);
            list[0].ProviderName.Should().Be("Tsumugi 作業所");
            list[1].TerminationDate.Should().Be(new DateOnly(2026, 12, 31));
        }
    }
}
