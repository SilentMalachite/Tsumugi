using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.UseCases.Claim;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;
using Tsumugi.Infrastructure.ClaimMasters;

namespace Tsumugi.Application.Tests.UseCases.Claim;

public sealed class QueryClaimBillingTokenOptionsCapabilityTests
{
    private static readonly QueryClaimBillingTokenOptionsUseCase UseCase =
        new(JsonClaimMasterProvider.LoadEmbedded());

    /// <summary>
    /// 体制届の選択番号はseedの条件定義にのみ存在し、UI/Applicationへハードコードしない
    /// （CLAUDE.md ハード制約3）。R6世代は(Ⅰ)〜(Ⅴ)＝option 2〜6。
    /// </summary>
    [Fact]
    public void R6_generation_exposes_options_two_through_six()
    {
        var dto = UseCase.Execute(new ServiceMonth(2024, 6));

        dto.TreatmentImprovementOptions.Should().Equal(2, 3, 4, 5, 6);
    }

    /// <summary>
    /// (Ⅴ)は2025-03限りで失効するため、2025-04以降のoption 6は選択肢から消える。
    /// </summary>
    [Fact]
    public void Category_v_disappears_after_march_2025()
    {
        UseCase.Execute(new ServiceMonth(2025, 3))
            .TreatmentImprovementOptions.Should().Contain(6);
        UseCase.Execute(new ServiceMonth(2025, 4))
            .TreatmentImprovementOptions.Should().NotContain(6);
    }

    /// <summary>
    /// R8世代は(Ⅰ)イ=2・(Ⅱ)イ=3・(Ⅲ)=4・(Ⅳ)=5・(Ⅰ)ロ=7・(Ⅱ)ロ=8。
    /// B型に(Ⅴ)は存在しないためoption 6は出ない（ADR 0048）。
    /// </summary>
    [Fact]
    public void R8_generation_exposes_the_six_reformed_options_without_category_v()
    {
        var dto = UseCase.Execute(new ServiceMonth(2026, 6));

        dto.TreatmentImprovementOptions.Should().Equal(2, 3, 4, 5, 7, 8);
    }

    /// <summary>
    /// (Ⅴ)区分の14択はR6の(Ⅴ)有効期間にのみ現れる。
    /// </summary>
    [Fact]
    public void The_category_v_band_options_exist_only_while_category_v_is_effective()
    {
        UseCase.Execute(new ServiceMonth(2024, 6))
            .TreatmentImprovementVBandOptions
            .Should().Equal(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14);

        UseCase.Execute(new ServiceMonth(2026, 6))
            .TreatmentImprovementVBandOptions.Should().BeEmpty();
    }

    /// <summary>
    /// <c>mhlw.b46.capability.treatment-improvement.</c>への接頭辞一致は
    /// <c>mhlw.b46.capability.treatment-improvement-v-band.</c>を拾ってはならない
    /// （接頭辞が"."で終端するため、直後がハイフンの後者は前者のプレフィックスに一致しない）。
    /// R6世代の実seedで、通常区分の選択番号域（2〜6）と(Ⅴ)区分の選択番号域（1〜14。7〜14は
    /// 通常区分に存在しない値）を使い、2ファミリーが互いに混入しないことを直接検査する。
    /// </summary>
    [Fact]
    public void Treatment_improvement_and_v_band_families_do_not_bleed_into_each_other()
    {
        var dto = UseCase.Execute(new ServiceMonth(2024, 6));

        // v-band専用の選択番号（7〜14）が通常区分側へ混入していない。
        dto.TreatmentImprovementOptions.Should().NotContain([7, 8, 9, 10, 11, 12, 13, 14]);

        // 通常区分の(Ⅴ)自体を示す選択番号6は、(Ⅴ)区分側（サブ区分1〜14）とは別の語彙であり
        // v-band側の選択肢には現れない（v-bandはサブ区分1〜14そのものを列挙する）。
        dto.TreatmentImprovementVBandOptions.Should().Equal(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14);
    }

    /// <summary>
    /// 合成データで2ファミリーの分離を直接検査する。接頭辞比較のバグ（末尾"."の欠落等）が
    /// 起きた場合、この合成データなら即座に検出できる: v-band専用の値9をtreatment-improvement側の
    /// 集合に、main専用の値2をv-band側の集合に、それぞれ一切含めてはならない。
    /// </summary>
    [Fact]
    public void Synthetic_condition_definitions_keep_the_two_capability_families_isolated()
    {
        var useCase = new QueryClaimBillingTokenOptionsUseCase(
            new FakeMasterProvider(SyntheticFamilyMasters()));

        var options = useCase.Execute(Month);

        options.TreatmentImprovementOptions.Should().Equal(2);
        options.TreatmentImprovementVBandOptions.Should().Equal(9);
    }

    private static readonly ServiceMonth Month = new(2024, 6);

    private static ClaimSourceRef SourceRef() => new(
        "doc-1",
        "0000000000000000000000000000000000000000000000000000000000000000",
        "loc",
        ClaimSourceEvidenceRole.Authoritative,
        [ClaimSourceSupport.Conditions]);

    private static ClaimCalculationMasterBundle SyntheticFamilyMasters() => new(
        BasicRewards: [],
        UnitAdjustments: [],
        RegionUnitPrices: [],
        BurdenCaps: [],
        TransitionRules: [],
        ServiceCodes: [],
        ConditionDefinitions:
        [
            new ClaimConditionDefinition(
                "cond-treatment-improvement-main", Month, null, ClaimConditionKind.OfficeCapability,
                ClaimConditionOperator.Equals,
                new ClaimConditionTokenOperand("mhlw.b46.capability.treatment-improvement.2"),
                [SourceRef()]),
            new ClaimConditionDefinition(
                "cond-treatment-improvement-v-band", Month, null, ClaimConditionKind.OfficeCapability,
                ClaimConditionOperator.Equals,
                new ClaimConditionTokenOperand("mhlw.b46.capability.treatment-improvement-v-band.9"),
                [SourceRef()]),
        ]);

    private sealed class FakeMasterProvider(ClaimCalculationMasterBundle masters) : IClaimMasterProvider
    {
        public ClaimMasterRelease ResolveVersion(ServiceMonth serviceMonth) =>
            throw new NotSupportedException();

        public ClaimCalculationMasterBundle ResolveCalculationMasters(ServiceMonth serviceMonth) => masters;
    }
}
