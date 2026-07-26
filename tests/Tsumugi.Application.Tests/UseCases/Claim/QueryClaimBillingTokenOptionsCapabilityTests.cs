using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.UseCases.Claim;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Tests.UseCases.Claim;

/// <summary>
/// 体制届の選択番号（<see cref="ClaimBillingTokenOptionsDto.TreatmentImprovementOptions"/> /
/// <see cref="ClaimBillingTokenOptionsDto.TreatmentImprovementVBandOptions"/>）が2ファミリーの
/// 接頭辞（<c>mhlw.b46.capability.treatment-improvement.</c> と
/// <c>mhlw.b46.capability.treatment-improvement-v-band.</c>）を混同しないことを、合成データで
/// 直接検査する。実R6-06 seedを対象月別に検証するテストは、Application層がInfrastructureを
/// 参照してはならない（依存方向厳守・<c>ArchitectureTests</c>）ため
/// <c>Tsumugi.Infrastructure.Tests.Claim.QueryClaimBillingTokenOptionsProductionWiringTests</c>に置く。
/// </summary>
public sealed class QueryClaimBillingTokenOptionsCapabilityTests
{
    private static readonly ServiceMonth Month = new(2024, 6);

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
