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
        // ADR 0047でFacilityClassificationは本スライスの対象になったため、この例は
        // EmploymentOutcomeCount（就労移行実績等、本スライス対象外のkind）に差し替える。
        // 他にも未配線のkindはあるが、代表として1つ検証すれば足りる。
        => FluentActions.Invoking(() => ServiceCodeResolver.ResolveBasicReward(
                SyntheticMastersWithFrozenConditionKind(), Month, DefaultContext()))
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

    /// <summary>
    /// ADR 0047: 施設区分条件は、context が施設区分を持たない（null）とき判定不能として
    /// フェイルクローズする。汎用の ConditionUnresolved ではなく専用コードで返し、
    /// 「施設区分が未入力である」ことを呼び出し側が判別できるようにする。
    /// 検証は公開API（<see cref="ServiceCodeResolver.ResolveAdditions"/>）経由で行う
    /// （<c>Evaluate</c>はprivateで<c>Tsumugi.Domain</c>にInternalsVisibleToが無いため）。
    /// </summary>
    [Fact]
    public void Facility_classification_condition_fails_closed_when_the_context_has_no_value()
    {
        var masters = SyntheticMastersWithFacilityClassificationAddition("designated-support-facility");
        var context = DefaultContext();

        var action = () => ServiceCodeResolver.ResolveAdditions(masters, Month, context);

        action.Should().Throw<ServiceCodeResolutionException>()
            .Which.Code.Should().Be(
                ServiceCodeResolutionErrorCode.FacilityClassificationUnresolved,
                "施設区分未入力は汎用の判定不能と区別する（ADR 0047）");
    }

    /// <summary>
    /// ADR 0047: 施設区分が入っていれば通常のtoken比較として評価する。一致した場合のみ加算行が
    /// 解決結果に含まれ、不一致の場合は「算定しない」として除外される（フェイルクローズではない）。
    /// </summary>
    [Theory]
    [InlineData("designated-support-facility", "designated-support-facility", true)]
    [InlineData("general", "designated-support-facility", false)]
    public void Facility_classification_condition_compares_the_token(
        string contextValue, string conditionValue, bool expected)
    {
        var masters = SyntheticMastersWithFacilityClassificationAddition(conditionValue);
        var context = DefaultContext() with { FacilityClassification = contextValue };

        var resolved = ServiceCodeResolver.ResolveAdditions(masters, Month, context);

        resolved.Should().HaveCount(expected ? 1 : 0);
    }

    /// <summary>
    /// I3: 施設区分未入力のフェイルクローズの影響範囲を、seedの条件配列の**並び順**で
    /// なく構造で決める。旧実装は <c>ConditionSelectors.All(...)</c> の短絡評価に頼っており、
    /// 処遇改善を届け出ていない事業所が例外に当たらないのは「施設区分セレクタが体制届
    /// セレクタより後ろに並んでいる」からに過ぎなかった（ADR 0048 決定5。validatorルールも
    /// テストも無し）。**どちらの並びでも**、他の条件が一致しない行は単に除外されること
    /// （＝throwしないこと）を固定する。
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void An_unresolved_facility_classification_does_not_throw_when_another_condition_fails(
        bool facilitySelectorFirst)
    {
        var masters = SyntheticMastersWithCapabilityAndFacilityAddition(facilitySelectorFirst);
        // 体制届キーを1件も宣言していない事業所（＝当該加算を算定しない）。施設区分は未入力。
        var context = DefaultContext() with
        {
            OfficeCapabilityKeys = new HashSet<string>(StringComparer.Ordinal),
            FacilityClassification = null,
        };

        var action = () => ServiceCodeResolver.ResolveAdditions(masters, Month, context);

        action.Should().NotThrow(
            "施設区分セレクタの並び順がフェイルクローズの影響範囲を決めてはならない");
        ServiceCodeResolver.ResolveAdditions(masters, Month, context).Should().BeEmpty();
    }

    /// <summary>
    /// I3: 逆に、他の条件がすべて一致する行では、並び順に依らずフェイルクローズする
    /// （ADR 0047・0048 の意図した取引を弱めていないことの確認）。
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void An_unresolved_facility_classification_still_fails_closed_when_every_other_condition_matches(
        bool facilitySelectorFirst)
    {
        var masters = SyntheticMastersWithCapabilityAndFacilityAddition(facilitySelectorFirst);
        var context = DefaultContext() with
        {
            OfficeCapabilityKeys = new HashSet<string>(StringComparer.Ordinal) { "capability-x" },
            FacilityClassification = null,
        };

        var action = () => ServiceCodeResolver.ResolveAdditions(masters, Month, context);

        action.Should().Throw<ServiceCodeResolutionException>()
            .Which.Code.Should().Be(ServiceCodeResolutionErrorCode.FacilityClassificationUnresolved);
    }

    /// <summary>
    /// I3用: 体制届条件と施設区分条件の**両方**を持つ加算行1本。
    /// <paramref name="facilitySelectorFirst"/> で条件配列の並びだけを入れ替える。
    /// </summary>
    private static ClaimCalculationMasterBundle SyntheticMastersWithCapabilityAndFacilityAddition(
        bool facilitySelectorFirst)
    {
        const string adjustmentKey = "addition.capability-and-facility-test";
        var amount = new FixedUnitsAmount(10);
        string[] selectors = facilitySelectorFirst
            ? ["cond-facility-addition", "cond-capability-x"]
            : ["cond-capability-x", "cond-facility-addition"];

        return new ClaimCalculationMasterBundle(
            BasicRewards: [],
            UnitAdjustments:
            [
                new UnitAdjustmentMasterRow(
                    adjustmentKey, amount, "step-add", null, BillingUnit.PerDay,
                    new ServiceMonth(2024, 4), null, [SourceRef()]),
            ],
            RegionUnitPrices: [],
            BurdenCaps: [],
            TransitionRules: [],
            ServiceCodes:
            [
                new ServiceCodeMasterRow(
                    "sc-capability-facility-addition",
                    "999998",
                    "体制届＋施設区分条件加算(合成)",
                    "b-type",
                    [],
                    selectors,
                    new UnitAdditionRule(adjustmentKey, amount, "step-add", null, BillingUnit.PerDay),
                    [
                        new ClaimComponentRef(
                            ClaimComponentMasterKind.Additions, adjustmentKey, ClaimComponentRole.Adjustment),
                    ],
                    new ServiceMonth(2024, 4),
                    null,
                    [SourceRef()]),
            ],
            ConditionDefinitions:
            [
                ConditionDefinition(
                    "cond-capability-x", ClaimConditionKind.OfficeCapability, ClaimConditionOperator.Equals,
                    new ClaimConditionTokenOperand("capability-x")),
                ConditionDefinition(
                    "cond-facility-addition", ClaimConditionKind.FacilityClassification,
                    ClaimConditionOperator.Equals,
                    new ClaimConditionTokenOperand("designated-support-facility")),
            ]);
    }

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

    // ADR 0047でFacilityClassificationは配線済みになったため、まだ配線されていないkind
    // （EmploymentOutcomeCount）で「凍結スコープはConditionUnresolved」の挙動を検証する。
    private static ClaimCalculationMasterBundle SyntheticMastersWithFrozenConditionKind() => new(
        BasicRewards: [BasicReward()],
        UnitAdjustments: [],
        RegionUnitPrices: [],
        BurdenCaps: [],
        TransitionRules: [],
        ServiceCodes:
        [
            ServiceCode("sc-a", "610000", [.. DefaultConditionSelectors, "cond-frozen"], "base-a"),
        ],
        ConditionDefinitions:
        [
            .. DefaultConditions(),
            ConditionDefinition(
                "cond-frozen", ClaimConditionKind.EmploymentOutcomeCount, ClaimConditionOperator.Equals,
                new ClaimConditionTokenOperand("unused")),
        ]);

    // ADR 0047: 施設区分条件つきの加算行1本のみを持つ最小マスタ。ResolveAdditions経由で
    // EvaluateFacilityClassificationの挙動（フェイルクローズ／token比較）を検証する。
    private static ClaimCalculationMasterBundle SyntheticMastersWithFacilityClassificationAddition(
        string conditionTokenValue)
    {
        const string adjustmentKey = "addition.facility-classification-test";
        var amount = new FixedUnitsAmount(10);

        return new ClaimCalculationMasterBundle(
            BasicRewards: [],
            UnitAdjustments:
            [
                new UnitAdjustmentMasterRow(
                    adjustmentKey, amount, "step-add", null, BillingUnit.PerDay,
                    new ServiceMonth(2024, 4), null, [SourceRef()]),
            ],
            RegionUnitPrices: [],
            BurdenCaps: [],
            TransitionRules: [],
            ServiceCodes:
            [
                new ServiceCodeMasterRow(
                    "sc-facility-addition",
                    "999999",
                    "施設区分条件加算(合成)",
                    "b-type",
                    [],
                    ["cond-facility-addition"],
                    new UnitAdditionRule(adjustmentKey, amount, "step-add", null, BillingUnit.PerDay),
                    [new ClaimComponentRef(ClaimComponentMasterKind.Additions, adjustmentKey, ClaimComponentRole.Adjustment)],
                    new ServiceMonth(2024, 4),
                    null,
                    [SourceRef()]),
            ],
            ConditionDefinitions:
            [
                ConditionDefinition(
                    "cond-facility-addition", ClaimConditionKind.FacilityClassification, ClaimConditionOperator.Equals,
                    new ClaimConditionTokenOperand(conditionTokenValue)),
            ]);
    }

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
