using FluentAssertions;
using Tsumugi.Domain.Logic.Claim;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Tests.Logic.Claim;

public sealed class ServiceCodeResolverTests
{
    private static readonly ServiceMonth Month = new(2025, 4);

    // 合成マスタは定員を頭数(int)で表す閾値条件（cond-cap-a: capacity, less-than-or-equal, 20）で
    // 表現する。Task 3のR6実値は5区分・8閾値条件（cap-20-or-less/cap-81-plusは単一、中間3区分は
    // 上下2条件）に再エンコードされているが、本テストは合成マスタなので単一の閾値条件で足りる。
    private static readonly string[] DefaultConditionSelectors =
        ["cond-system-b", "cond-band-a", "cond-cap-a", "cond-staff-a"];

    [Fact]
    public void Resolves_the_single_matching_service_code_to_base_units()
    {
        var masters = SyntheticMasters();
        var context = DefaultContext();

        var resolved = ServiceCodeResolver.ResolveBasicReward(masters, Month, context);

        resolved.ServiceCode.Should().Be("610000");
        resolved.UnitsPerDay.Should().Be(700);
        resolved.BillingUnit.Should().Be(BillingUnit.PerDay);
    }

    [Fact]
    public void Throws_when_no_service_code_matches()
        => FluentActions.Invoking(() => ServiceCodeResolver.ResolveBasicReward(
                SyntheticMasters(), Month, ContextWith(paymentBand: "band-unknown")))
            .Should().Throw<ServiceCodeResolutionException>()
            .Which.Code.Should().Be(ServiceCodeResolutionErrorCode.MasterUnavailable);

    [Fact]
    public void Throws_when_capacity_headcount_is_outside_the_threshold_condition()
        => FluentActions.Invoking(() => ServiceCodeResolver.ResolveBasicReward(
                SyntheticMasters(), Month, ContextWith(capacityHeadcount: 21)))
            .Should().Throw<ServiceCodeResolutionException>()
            .Which.Code.Should().Be(ServiceCodeResolutionErrorCode.MasterUnavailable);

    [Fact]
    public void Throws_ambiguous_when_two_service_codes_match()
        => FluentActions.Invoking(() => ServiceCodeResolver.ResolveBasicReward(
                SyntheticMastersWithDuplicateMatch(), Month, DefaultContext()))
            .Should().Throw<ServiceCodeResolutionException>()
            .Which.Code.Should().Be(ServiceCodeResolutionErrorCode.AmbiguousMatch);

    [Fact]
    public void Throws_condition_unresolved_for_frozen_condition_kinds()
        // FacilityClassification（保護施設系）等、本スライス対象外のkindを含む行はConditionUnresolved
        => FluentActions.Invoking(() => ServiceCodeResolver.ResolveBasicReward(
                SyntheticMastersWithFacilityClassificationCondition(), Month, DefaultContext()))
            .Should().Throw<ServiceCodeResolutionException>()
            .Which.Code.Should().Be(ServiceCodeResolutionErrorCode.ConditionUnresolved);

    [Fact]
    public void Throws_component_missing_when_base_component_ref_is_broken()
        => FluentActions.Invoking(() => ServiceCodeResolver.ResolveBasicReward(
                SyntheticMastersWithBrokenComponentRef(), Month, DefaultContext()))
            .Should().Throw<ServiceCodeResolutionException>()
            .Which.Code.Should().Be(ServiceCodeResolutionErrorCode.ComponentMissing);

    [Fact]
    public void Throws_condition_unresolved_when_duplicate_condition_definition_exists()
        => FluentActions.Invoking(() => ServiceCodeResolver.ResolveBasicReward(
                SyntheticMastersWithDuplicateConditionDefinition(), Month, DefaultContext()))
            .Should().Throw<ServiceCodeResolutionException>()
            .Which.Code.Should().Be(ServiceCodeResolutionErrorCode.ConditionUnresolved);

    [Fact]
    public void Throws_ambiguous_match_when_duplicate_basic_reward_key_exists()
        => FluentActions.Invoking(() => ServiceCodeResolver.ResolveBasicReward(
                SyntheticMastersWithDuplicateBasicRewardKey(), Month, DefaultContext()))
            .Should().Throw<ServiceCodeResolutionException>()
            .Which.Code.Should().Be(ServiceCodeResolutionErrorCode.AmbiguousMatch);

    [Fact]
    public void Throws_unsupported_unit_rule_when_service_code_uses_fixed_composite_rule()
        => FluentActions.Invoking(() => ServiceCodeResolver.ResolveBasicReward(
                SyntheticMastersWithFixedCompositeUnitRule(), Month, DefaultContext()))
            .Should().Throw<ServiceCodeResolutionException>()
            .Which.Code.Should().Be(ServiceCodeResolutionErrorCode.UnsupportedUnitRule);

    [Fact]
    public void Resolves_when_token_condition_uses_in_operator_with_matching_token()
        => FluentActions.Invoking(() => ServiceCodeResolver.ResolveBasicReward(
                SyntheticMastersWithInOperatorCondition(), Month, DefaultContext()))
            .Should().NotThrow();

    private static ClaimBillingConditionContext DefaultContext() => new(
        RewardSystem: "b-type",
        PaymentBand: "band-a",
        CapacityHeadcount: 20,
        StaffingKey: "staff-a",
        AverageWageBandOption: new AverageWageBandOption(AverageWageBandOptionKind.Numeric, 3),
        R8ReformStatus: R8ReformStatus.NotApplicableBeforeR8);

    private static ClaimBillingConditionContext ContextWith(
        string? paymentBand = null, int? capacityHeadcount = null) => DefaultContext() with
        {
            PaymentBand = paymentBand ?? DefaultContext().PaymentBand,
            CapacityHeadcount = capacityHeadcount ?? DefaultContext().CapacityHeadcount,
        };

    private static ClaimSourceRef SourceRef() => new(
        "doc-1",
        "0000000000000000000000000000000000000000000000000000000000000",
        "loc",
        ClaimSourceEvidenceRole.Authoritative,
        [ClaimSourceSupport.Conditions, ClaimSourceSupport.EffectivePeriod]);

    private static BasicRewardMasterRow BasicReward(
        string key = "base-a", string serviceCode = "610000") => new(
        key, "band-a", "staff-a", "cap-a", serviceCode, 700,
        new ServiceMonth(2024, 4), null, [SourceRef()]);

    private static ClaimConditionDefinition ConditionDefinition(
        string key, ClaimConditionKind kind, ClaimConditionOperator @operator, ClaimConditionOperand operand) => new(
        key, new ServiceMonth(2024, 4), null, kind, @operator, operand, [SourceRef()]);

    private static ClaimConditionDefinition[] DefaultConditions() =>
    [
        ConditionDefinition(
            "cond-system-b", ClaimConditionKind.RewardSystem, ClaimConditionOperator.Equals,
            new ClaimConditionTokenOperand("b-type")),
        ConditionDefinition(
            "cond-band-a", ClaimConditionKind.PaymentBand, ClaimConditionOperator.Equals,
            new ClaimConditionTokenOperand("band-a")),
        ConditionDefinition(
            "cond-cap-a", ClaimConditionKind.Capacity, ClaimConditionOperator.LessThanOrEqual,
            new ClaimConditionIntegerOperand(20)),
        ConditionDefinition(
            "cond-staff-a", ClaimConditionKind.Staffing, ClaimConditionOperator.Equals,
            new ClaimConditionTokenOperand("staff-a")),
    ];

    private static ServiceCodeMasterRow ServiceCode(
        string key,
        string serviceCode,
        IReadOnlyList<string> conditionSelectors,
        string baseComponentKey) => new(
        key,
        serviceCode,
        "B型基本(合成)",
        "b-type",
        [],
        conditionSelectors,
        new BaseComponentPassThroughRule(baseComponentKey, "step-base", null, BillingUnit.PerDay),
        [new ClaimComponentRef(ClaimComponentMasterKind.BasicRewards, baseComponentKey, ClaimComponentRole.Base)],
        new ServiceMonth(2024, 4),
        null,
        [SourceRef()]);

    private static ClaimCalculationMasterBundle SyntheticMasters() => new(
        BasicRewards: [BasicReward()],
        UnitAdjustments: [],
        RegionUnitPrices: [],
        BurdenCaps: [],
        TransitionRules: [],
        ServiceCodes: [ServiceCode("sc-a", "610000", DefaultConditionSelectors, "base-a")],
        ConditionDefinitions: DefaultConditions());

    private static ClaimCalculationMasterBundle SyntheticMastersWithDuplicateMatch() => new(
        BasicRewards: [BasicReward(), BasicReward(key: "base-b", serviceCode: "620000")],
        UnitAdjustments: [],
        RegionUnitPrices: [],
        BurdenCaps: [],
        TransitionRules: [],
        ServiceCodes:
        [
            ServiceCode("sc-a", "610000", DefaultConditionSelectors, "base-a"),
            ServiceCode("sc-b", "620000", DefaultConditionSelectors, "base-b"),
        ],
        ConditionDefinitions: DefaultConditions());

    private static ClaimCalculationMasterBundle SyntheticMastersWithFacilityClassificationCondition() => new(
        BasicRewards: [BasicReward()],
        UnitAdjustments: [],
        RegionUnitPrices: [],
        BurdenCaps: [],
        TransitionRules: [],
        ServiceCodes:
        [
            ServiceCode("sc-a", "610000", [.. DefaultConditionSelectors, "cond-facility"], "base-a"),
        ],
        ConditionDefinitions:
        [
            .. DefaultConditions(),
            ConditionDefinition(
                "cond-facility", ClaimConditionKind.FacilityClassification, ClaimConditionOperator.Equals,
                new ClaimConditionTokenOperand("protected")),
        ]);

    private static ClaimCalculationMasterBundle SyntheticMastersWithBrokenComponentRef() => new(
        BasicRewards: [BasicReward()],
        UnitAdjustments: [],
        RegionUnitPrices: [],
        BurdenCaps: [],
        TransitionRules: [],
        ServiceCodes: [ServiceCode("sc-a", "610000", DefaultConditionSelectors, "base-missing")],
        ConditionDefinitions: DefaultConditions());

    private static ClaimCalculationMasterBundle SyntheticMastersWithDuplicateConditionDefinition() => new(
        BasicRewards: [BasicReward()],
        UnitAdjustments: [],
        RegionUnitPrices: [],
        BurdenCaps: [],
        TransitionRules: [],
        ServiceCodes: [ServiceCode("sc-a", "610000", DefaultConditionSelectors, "base-a")],
        ConditionDefinitions:
        [
            .. DefaultConditions(),
            ConditionDefinition(
                "cond-system-b", ClaimConditionKind.RewardSystem, ClaimConditionOperator.Equals,
                new ClaimConditionTokenOperand("b-type")),
        ]);

    private static ClaimCalculationMasterBundle SyntheticMastersWithDuplicateBasicRewardKey() => new(
        BasicRewards:
        [
            BasicReward(key: "base-a", serviceCode: "610000"),
            BasicReward(key: "base-a", serviceCode: "620000"),
        ],
        UnitAdjustments: [],
        RegionUnitPrices: [],
        BurdenCaps: [],
        TransitionRules: [],
        ServiceCodes: [ServiceCode("sc-a", "610000", DefaultConditionSelectors, "base-a")],
        ConditionDefinitions: DefaultConditions());

    private static ClaimCalculationMasterBundle SyntheticMastersWithFixedCompositeUnitRule() => new(
        BasicRewards: [BasicReward()],
        UnitAdjustments: [],
        RegionUnitPrices: [],
        BurdenCaps: [],
        TransitionRules: [],
        ServiceCodes:
        [
            new ServiceCodeMasterRow(
                "sc-a",
                "610000",
                "B型基本(合成)",
                "b-type",
                [],
                DefaultConditionSelectors,
                new FixedCompositeUnitRule(500, BillingUnit.PerDay),
                [new ClaimComponentRef(ClaimComponentMasterKind.BasicRewards, "base-a", ClaimComponentRole.Base)],
                new ServiceMonth(2024, 4),
                null,
                [SourceRef()]),
        ],
        ConditionDefinitions: DefaultConditions());

    private static ClaimCalculationMasterBundle SyntheticMastersWithInOperatorCondition() => new(
        BasicRewards: [BasicReward()],
        UnitAdjustments: [],
        RegionUnitPrices: [],
        BurdenCaps: [],
        TransitionRules: [],
        ServiceCodes:
        [
            ServiceCode("sc-a", "610000",
                ["cond-system-in", "cond-band-a", "cond-cap-a", "cond-staff-a"], "base-a"),
        ],
        ConditionDefinitions:
        [
            ConditionDefinition(
                "cond-system-in", ClaimConditionKind.RewardSystem, ClaimConditionOperator.In,
                new ClaimConditionTokenSetOperand(["b-type", "c-type"])),
            ConditionDefinition(
                "cond-band-a", ClaimConditionKind.PaymentBand, ClaimConditionOperator.Equals,
                new ClaimConditionTokenOperand("band-a")),
            ConditionDefinition(
                "cond-cap-a", ClaimConditionKind.Capacity, ClaimConditionOperator.LessThanOrEqual,
                new ClaimConditionIntegerOperand(20)),
            ConditionDefinition(
                "cond-staff-a", ClaimConditionKind.Staffing, ClaimConditionOperator.Equals,
                new ClaimConditionTokenOperand("staff-a")),
        ]);
}
