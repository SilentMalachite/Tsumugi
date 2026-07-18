using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.UseCases.Claim;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Application.Tests.UseCases.Claim;

/// <summary>
/// StaffingKey/RegionKeyの入力欄向け選択肢は、マスタのstaffing条件token・region-unit-price行
/// から列挙する（ハードコードしない。Task 9b）。
/// </summary>
public sealed class QueryClaimBillingTokenOptionsUseCaseTests
{
    private static readonly ServiceMonth Month = new(2024, 4);

    [Fact]
    public void Execute_lists_distinct_staffing_tokens_from_staffing_conditions()
    {
        var useCase = new QueryClaimBillingTokenOptionsUseCase(
            new FakeMasterProvider(Masters()));

        var options = useCase.Execute(Month);

        options.StaffingKeyOptions.Should().BeEquivalentTo(["staff-6-1", "staff-7.5-1", "staff-a"]);
    }

    [Fact]
    public void Execute_lists_distinct_region_keys_from_region_unit_prices()
    {
        var useCase = new QueryClaimBillingTokenOptionsUseCase(
            new FakeMasterProvider(Masters()));

        var options = useCase.Execute(Month);

        options.RegionKeyOptions.Should().BeEquivalentTo(["region-a", "region-grade-2"]);
    }

    [Fact]
    public void Execute_returns_empty_options_when_master_is_unavailable()
    {
        var useCase = new QueryClaimBillingTokenOptionsUseCase(new UnavailableMasterProvider());

        var options = useCase.Execute(Month);

        options.StaffingKeyOptions.Should().BeEmpty();
        options.RegionKeyOptions.Should().BeEmpty();
    }

    private static ClaimSourceRef SourceRef() => new(
        "doc-1",
        "0000000000000000000000000000000000000000000000000000000000000000",
        "loc",
        ClaimSourceEvidenceRole.Authoritative,
        [ClaimSourceSupport.Conditions]);

    private static ClaimCalculationMasterBundle Masters() => new(
        BasicRewards: [],
        UnitAdjustments: [],
        RegionUnitPrices:
        [
            new RegionUnitPriceMasterRow(
                "price-a", "region-a", "b-type", 10.00m, Month, null, [SourceRef()]),
            new RegionUnitPriceMasterRow(
                "price-b", "region-grade-2", "b-type", 10.91m, Month, null, [SourceRef()]),
            new RegionUnitPriceMasterRow(
                "price-a-dup", "region-a", "b-type", 10.00m, Month, null, [SourceRef()]),
        ],
        BurdenCaps: [],
        TransitionRules: [],
        ServiceCodes: [],
        ConditionDefinitions:
        [
            new ClaimConditionDefinition(
                "cond-staff-a", Month, null, ClaimConditionKind.Staffing,
                ClaimConditionOperator.Equals, new ClaimConditionTokenOperand("staff-a"), [SourceRef()]),
            new ClaimConditionDefinition(
                "cond-staff-set", Month, null, ClaimConditionKind.Staffing,
                ClaimConditionOperator.In,
                new ClaimConditionTokenSetOperand(["staff-6-1", "staff-7.5-1"]), [SourceRef()]),
            new ClaimConditionDefinition(
                "cond-staff-a-dup", Month, null, ClaimConditionKind.Staffing,
                ClaimConditionOperator.Equals, new ClaimConditionTokenOperand("staff-a"), [SourceRef()]),
            new ClaimConditionDefinition(
                "cond-system-b", Month, null, ClaimConditionKind.RewardSystem,
                ClaimConditionOperator.Equals, new ClaimConditionTokenOperand("b-type"), [SourceRef()]),
        ]);

    private sealed class FakeMasterProvider(ClaimCalculationMasterBundle masters) : IClaimMasterProvider
    {
        public ClaimMasterRelease ResolveVersion(ServiceMonth serviceMonth) =>
            throw new NotSupportedException();

        public ClaimCalculationMasterBundle ResolveCalculationMasters(ServiceMonth serviceMonth) => masters;
    }

    private sealed class UnavailableMasterProvider : IClaimMasterProvider
    {
        public ClaimMasterRelease ResolveVersion(ServiceMonth serviceMonth) =>
            throw new NotSupportedException();

        public ClaimCalculationMasterBundle ResolveCalculationMasters(ServiceMonth serviceMonth) =>
            throw new ClaimMasterPolicyUnavailableException(ClaimMasterPolicyUnavailableCode.Unavailable);
    }
}
