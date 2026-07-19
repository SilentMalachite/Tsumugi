using FluentAssertions;
using Tsumugi.Domain.Logic.Claim;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Tests.Logic.Claim;

/// <summary>
/// Task 11（ADR 0028）: 加算明細の算定セマンティクスを合成マスタで固定する。
/// マスタ値・トークンはすべて合成（production seedの正準文字列はDomainテストへ複製しない。
/// <c>ClaimSpecificationBoundaryTests</c>の検査対象はproductionソースのみだが、意図としても
/// seed実値はgolden caseテストの再掲値に限定する）。
/// <list type="bullet">
/// <item>体制条件（<see cref="ClaimConditionKind.OfficeCapability"/>）: 実効な体制届キー集合に
/// 含まれる場合のみ加算行が成立する。キー集合が<c>null</c>（未取得）ならフェイルクローズ。</item>
/// <item><see cref="UnitsPerCountAmount"/>: 整数単位×回数（丸めなし）。回数は
/// <see cref="ClaimCalculationRequest.CountSelectorBindings"/>で束縛された
/// <see cref="RecipientClaimSource"/>の型付き実績フィールドから取る。</item>
/// <item><see cref="FixedUnitsAmount"/>: 1日につき=単位×請求日数、1月につき=単位×1。</item>
/// </list>
/// </summary>
public sealed class ClaimCalculatorAdditionTests
{
    private static readonly ServiceMonth Month = new(2025, 4);
    private static readonly Guid RecipientA = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private const int SyntheticCapYen = 9_999_999;

    private const string CapabilityWpsI = "cap.staffing-addition.a";
    private const string CapabilityWpsII = "cap.staffing-addition.b";

    private static ClaimBillingConditionContext Context(
        IReadOnlyCollection<string>? capabilityKeys) => new(
        RewardSystem: "b-type",
        PaymentBand: "band-x",
        CapacityHeadcount: 20,
        StaffingKey: "staff-x",
        AverageWageBandOption: new AverageWageBandOption(AverageWageBandOptionKind.Numeric, 5),
        R8ReformStatus: R8ReformStatus.NotApplicableBeforeR8,
        OfficeCapabilityKeys: capabilityKeys);

    private static Dictionary<string, ClaimCountMetric> Bindings() =>
        new Dictionary<string, ClaimCountMetric>(StringComparer.Ordinal)
        {
            ["count-days"] = ClaimCountMetric.ServiceDays,
        };

    private static ClaimCalculationRequest Request(
        IReadOnlyCollection<string>? capabilityKeys,
        int billedDays = 20,
        IReadOnlyDictionary<string, ClaimCountMetric>? bindings = null) => new(
        Month,
        Context(capabilityKeys),
        "region-x",
        "b-type",
        [new RecipientClaimSource(RecipientA, billedDays, 90, SyntheticCapYen)],
        bindings ?? Bindings());

    [Fact]
    public void Per_day_count_addition_applies_when_office_holds_the_capability()
    {
        var result = ClaimCalculator.Calculate(Masters(), Request([CapabilityWpsI]));

        var detail = result.Details.Should().ContainSingle().Subject;
        // 基本 700×20=14,000 + 加算 15×20=300
        detail.TotalUnits.Should().Be(14_300);
        var line = detail.AdditionLines.Should().ContainSingle().Subject;
        line.ServiceCode.Should().Be("610101");
        line.OfficialLabel.Should().Be("加算Ａ（Ⅰ）");
        line.Units.Should().Be(300);
        // ADR 0025: 総費用額=14,300×10.00=143,000円、1割相当=14,300円、給付費=128,700円。
        detail.TotalCostYen.Should().Be(143_000);
        detail.BurdenYen.Should().Be(14_300);
        detail.BenefitYen.Should().Be(128_700);
    }

    [Fact]
    public void Addition_is_not_billed_when_office_does_not_hold_the_capability()
    {
        var result = ClaimCalculator.Calculate(Masters(), Request(capabilityKeys: []));

        var detail = result.Details.Should().ContainSingle().Subject;
        detail.TotalUnits.Should().Be(14_000);
        detail.AdditionLines.Should().BeEmpty();
    }

    [Fact]
    public void One_hot_capability_selects_only_the_matching_variant()
    {
        var result = ClaimCalculator.Calculate(Masters(), Request([CapabilityWpsII]));

        var detail = result.Details.Should().ContainSingle().Subject;
        // 加算Ａ（Ⅱ）: 10×20=200のみ。Ⅰ（15単位）は体制外。
        detail.TotalUnits.Should().Be(14_200);
        detail.AdditionLines.Should().ContainSingle().Which.ServiceCode.Should().Be("610102");
    }

    [Fact]
    public void Capability_condition_fails_closed_when_capability_keys_are_unavailable()
    {
        var act = () => ClaimCalculator.Calculate(Masters(), Request(capabilityKeys: null));

        act.Should().Throw<ServiceCodeResolutionException>()
            .Which.Code.Should().Be(ServiceCodeResolutionErrorCode.ConditionUnresolved);
    }

    [Fact]
    public void Count_addition_fails_closed_when_count_selector_binding_is_missing()
    {
        var act = () => ClaimCalculator.Calculate(
            Masters(),
            Request(
                [CapabilityWpsI],
                bindings: new Dictionary<string, ClaimCountMetric>(StringComparer.Ordinal)));

        act.Should().Throw<ClaimCalculationException>()
            .Which.Code.Should().Be(ClaimCalculationErrorCode.UnsupportedAdditionRule);
    }

    [Fact]
    public void Addition_fails_closed_when_component_row_is_missing()
    {
        var masters = Masters() with { UnitAdjustments = [] };

        var act = () => ClaimCalculator.Calculate(masters, Request([CapabilityWpsI]));

        act.Should().Throw<ServiceCodeResolutionException>()
            .Which.Code.Should().Be(ServiceCodeResolutionErrorCode.ComponentMissing);
    }

    [Fact]
    public void Addition_fails_closed_when_component_row_amount_disagrees_with_the_rule()
    {
        var masters = Masters();
        var mismatched = masters with
        {
            UnitAdjustments =
            [
                Adjustment(
                    "adj-a1",
                    new UnitsPerCountAmount(14, "count-days"),
                    BillingUnit.PerDay),
                masters.UnitAdjustments[1],
            ],
        };

        var act = () => ClaimCalculator.Calculate(mismatched, Request([CapabilityWpsI]));

        act.Should().Throw<ServiceCodeResolutionException>()
            .Which.Code.Should().Be(ServiceCodeResolutionErrorCode.ComponentMismatch);
    }

    [Fact]
    public void Fixed_units_amount_bills_per_day_and_per_month_billing_units()
    {
        var masters = MastersWithFixedAdditions();

        var result = ClaimCalculator.Calculate(masters, Request([CapabilityWpsI], billedDays: 20));

        var detail = result.Details.Should().ContainSingle().Subject;
        // per-day 30×20=600、per-month 55×1=55。
        detail.AdditionLines.Should().HaveCount(2);
        detail.TotalUnits.Should().Be(14_000 + 600 + 55);
    }

    [Fact]
    public void Zero_count_produces_no_addition_line()
    {
        // BilledDays>0は基本報酬の前提のためServiceDaysでは0回を作れない。
        // 束縛先を欠席時対応系フィールド（既定0）へ差し替えて0回を表す。
        var bindings = new Dictionary<string, ClaimCountMetric>(StringComparer.Ordinal)
        {
            ["count-days"] = ClaimCountMetric.AbsenceSupport,
        };

        var result = ClaimCalculator.Calculate(
            Masters(), Request([CapabilityWpsI], bindings: bindings));

        result.Details.Should().ContainSingle().Which.AdditionLines.Should().BeEmpty();
    }

    [Theory]
    [InlineData(20, "610401", 45 * 20)]
    [InlineData(30, "610402", 40 * 20)]
    public void Capacity_banded_addition_variant_follows_the_office_headcount(
        int capacityHeadcount, string expectedServiceCode, int expectedUnits)
    {
        // 目標工賃達成指導員配置加算型（ADR 0028決定3）: 定員区分ごとの行を頭数の閾値条件で選ぶ。
        // 閾値はマスタ条件（ClaimConditionKind.Capacity）にのみ置く。
        var amount45 = new UnitsPerCountAmount(45, "count-days");
        var amount40 = new UnitsPerCountAmount(40, "count-days");
        var masters = Masters() with
        {
            UnitAdjustments =
            [
                Adjustment("adj-twi-small", amount45, BillingUnit.PerDay),
                Adjustment("adj-twi-mid", amount40, BillingUnit.PerDay),
            ],
            ServiceCodes =
            [
                Masters().ServiceCodes[0],
                AdditionService(
                    "svc-twi-small", "610401", "指導員配置（小）", "adj-twi-small", amount45,
                    BillingUnit.PerDay, ["cond-system-b", "cond-cap-a1", "cond-cap-small"]),
                AdditionService(
                    "svc-twi-mid", "610402", "指導員配置（中）", "adj-twi-mid", amount40,
                    BillingUnit.PerDay, ["cond-system-b", "cond-cap-a1", "cond-cap-mid-gte", "cond-cap-mid-lte"]),
            ],
            ConditionDefinitions =
            [
                .. Masters().ConditionDefinitions,
                new ClaimConditionDefinition(
                    "cond-cap-small", new ServiceMonth(2024, 4), null,
                    ClaimConditionKind.Capacity, ClaimConditionOperator.LessThanOrEqual,
                    new ClaimConditionIntegerOperand(20), [SourceRef()]),
                new ClaimConditionDefinition(
                    "cond-cap-mid-gte", new ServiceMonth(2024, 4), null,
                    ClaimConditionKind.Capacity, ClaimConditionOperator.GreaterThanOrEqual,
                    new ClaimConditionIntegerOperand(21), [SourceRef()]),
                new ClaimConditionDefinition(
                    "cond-cap-mid-lte", new ServiceMonth(2024, 4), null,
                    ClaimConditionKind.Capacity, ClaimConditionOperator.LessThanOrEqual,
                    new ClaimConditionIntegerOperand(40), [SourceRef()]),
            ],
        };
        var request = new ClaimCalculationRequest(
            Month,
            Context([CapabilityWpsI]) with { CapacityHeadcount = capacityHeadcount },
            "region-x",
            "b-type",
            [new RecipientClaimSource(RecipientA, 20, 90, SyntheticCapYen)],
            Bindings());

        var result = ClaimCalculator.Calculate(masters, request);

        var line = result.Details.Should().ContainSingle()
            .Which.AdditionLines.Should().ContainSingle().Subject;
        line.ServiceCode.Should().Be(expectedServiceCode);
        line.Units.Should().Be(expectedUnits);
    }

    [Fact]
    public void Initial_period_addition_bills_days_within_the_initial_window_without_a_capability()
    {
        // 初期加算（ADR 0028決定3）: 体制届出不要・利用開始日から30日以内のサービス提供日数×30単位。
        // 利用開始日ストレージは未実装（決定5 gap）のためproduction seedには行を置かず、
        // セマンティクスのみ合成マスタで固定する（ClaimAdditionSeedScopeTestsが除外をpin）。
        var amount = new UnitsPerCountAmount(30, "count-initial");
        var masters = Masters() with
        {
            UnitAdjustments = [Adjustment("adj-init", amount, BillingUnit.PerDay)],
            ServiceCodes =
            [
                Masters().ServiceCodes[0],
                AdditionService(
                    "svc-init", "610301", "初期加算", "adj-init", amount, BillingUnit.PerDay,
                    ["cond-system-b"]),
            ],
        };
        var request = new ClaimCalculationRequest(
            Month,
            Context([]),
            "region-x",
            "b-type",
            [new RecipientClaimSource(RecipientA, 20, 90, SyntheticCapYen, InitialPeriodServiceDays: 10)],
            new Dictionary<string, ClaimCountMetric>(StringComparer.Ordinal)
            {
                ["count-initial"] = ClaimCountMetric.InitialPeriodServiceDays,
            });

        var result = ClaimCalculator.Calculate(masters, request);

        var detail = result.Details.Should().ContainSingle().Subject;
        // 基本 700×20=14,000 + 初期 30×10=300。
        detail.TotalUnits.Should().Be(14_300);
        detail.AdditionLines.Should().ContainSingle().Which.Units.Should().Be(300);
    }

    [Fact]
    public void Initial_period_days_beyond_billed_days_are_rejected()
    {
        var request = new ClaimCalculationRequest(
            Month,
            Context([]),
            "region-x",
            "b-type",
            [new RecipientClaimSource(RecipientA, 20, 90, SyntheticCapYen, InitialPeriodServiceDays: 21)],
            Bindings());

        var act = () => ClaimCalculator.Calculate(Masters(), request);

        act.Should().Throw<ClaimCalculationException>()
            .Which.Code.Should().Be(ClaimCalculationErrorCode.InvalidInput);
    }

    [Theory]
    [InlineData(2, 94 * 2)]
    [InlineData(4, 94 * 4)]
    [InlineData(6, 94 * 4)] // 月4回を上限にcap（上限値はマスタ行のMonthlyCountCapのみが運ぶ）
    public void Absence_response_count_is_capped_by_the_master_monthly_count_cap(
        int absenceSupportCount, int expectedUnits)
    {
        // 欠席時対応加算型（ADR 0028決定3: 1回につき・月4回限度・体制届出不要）。
        var amount = new UnitsPerCountAmount(94, "count-absence", MonthlyCountCap: 4);
        var masters = Masters() with
        {
            UnitAdjustments = [Adjustment("adj-absence", amount, BillingUnit.PerUse)],
            ServiceCodes =
            [
                Masters().ServiceCodes[0],
                AdditionService(
                    "svc-absence", "610501", "欠席時対応", "adj-absence", amount, BillingUnit.PerUse,
                    ["cond-system-b"]),
            ],
        };
        var request = new ClaimCalculationRequest(
            Month,
            Context([]),
            "region-x",
            "b-type",
            [
                new RecipientClaimSource(
                    RecipientA, 20, 90, SyntheticCapYen, AbsenceSupportCount: absenceSupportCount),
            ],
            new Dictionary<string, ClaimCountMetric>(StringComparer.Ordinal)
            {
                ["count-absence"] = ClaimCountMetric.AbsenceSupport,
            });

        var result = ClaimCalculator.Calculate(masters, request);

        result.Details.Should().ContainSingle()
            .Which.AdditionLines.Should().ContainSingle()
            .Which.Units.Should().Be(expectedUnits);
    }

    [Fact]
    public void Same_premises_transport_uses_its_own_one_way_count_metric()
    {
        // 送迎加算の同一敷地variant（ADR 0028決定3）: 合成済み固定単位×同一敷地片道回数。
        // 同一敷地判別のストレージは未実装（決定5 gap）のためproduction seedには行を置かず、
        // セマンティクスのみ固定する（ClaimAdditionSeedScopeTestsが除外をpin）。
        var amount = new UnitsPerCountAmount(7, "count-transport-same");
        var masters = Masters() with
        {
            UnitAdjustments = [Adjustment("adj-tsp", amount, BillingUnit.PerUse)],
            ServiceCodes =
            [
                Masters().ServiceCodes[0],
                AdditionService(
                    "svc-tsp", "610601", "送迎（同一敷地）", "adj-tsp", amount, BillingUnit.PerUse,
                    ["cond-system-b", "cond-cap-a1"]),
            ],
        };
        var request = new ClaimCalculationRequest(
            Month,
            Context([CapabilityWpsI]),
            "region-x",
            "b-type",
            [
                new RecipientClaimSource(
                    RecipientA, 20, 90, SyntheticCapYen,
                    TransportOneWayCount: 5,
                    TransportSamePremisesOneWayCount: 36),
            ],
            new Dictionary<string, ClaimCountMetric>(StringComparer.Ordinal)
            {
                ["count-transport-same"] = ClaimCountMetric.TransportOneWaySamePremises,
            });

        var result = ClaimCalculator.Calculate(masters, request);

        // 通常送迎の片道回数（5）ではなく同一敷地片道回数（36）に束縛される。
        result.Details.Should().ContainSingle()
            .Which.AdditionLines.Should().ContainSingle()
            .Which.Units.Should().Be(7 * 36);
    }

    private static ClaimCalculationMasterBundle MastersWithPercentage(
        params (string Key, string Code, decimal Percentage, int Order)[] percentageRows)
    {
        // 基本行＋固定単位行（Ⅰ=15単位/日）にtargetトークンを付与し、%行は付与しない
        // （%行自身は月次対象合計に含まない=複利にしない。ADR 0025/0028）。
        var masters = Masters();
        var baseRow = masters.ServiceCodes[0] with
        {
            Selectors = ["selector:svc-base", "tgt-a"],
        };
        var fixedRow = masters.ServiceCodes[1] with
        {
            Selectors = ["selector:svc-add-a1", "tgt-a"],
        };
        var adjustments = new List<UnitAdjustmentMasterRow>
        {
            masters.UnitAdjustments[0],
        };
        var services = new List<ServiceCodeMasterRow> { baseRow, fixedRow };
        foreach (var (key, code, percentage, order) in percentageRows)
        {
            var amount = new PercentageOfTargetAmount(
                percentage,
                PercentageApplicationKind.Add,
                PercentageBaseScope.MonthlyTargetUnitSum,
                "tgt-a",
                order);
            adjustments.Add(new UnitAdjustmentMasterRow(
                key, amount, "step-pct", "claim.rounding.units.half-up.v1", BillingUnit.PerMonth,
                new ServiceMonth(2024, 4), null, [SourceRef()]));
            services.Add(new ServiceCodeMasterRow(
                $"svc-{key}", code, $"割合加算{code}", "b-type", [$"selector:svc-{key}"],
                ["cond-system-b", "cond-cap-a1"],
                new UnitAdditionRule(
                    key, amount, "step-pct", "claim.rounding.units.half-up.v1", BillingUnit.PerMonth),
                [new ClaimComponentRef(ClaimComponentMasterKind.Additions, key, ClaimComponentRole.Adjustment)],
                new ServiceMonth(2024, 4), null, [SourceRef()]));
        }

        return masters with
        {
            UnitAdjustments = adjustments,
            ServiceCodes = services,
        };
    }

    [Fact]
    public void Percentage_addition_applies_to_the_monthly_target_sum_with_half_up_rounding()
    {
        // 対象合計 = 基本 700×20 + 固定 15×20 = 14,300。9.3% = 1,329.9 → 半上げ → 1,330。
        var masters = MastersWithPercentage(("adj-pct-a", "610901", 0.093m, 1));

        var result = ClaimCalculator.Calculate(masters, Request([CapabilityWpsI]));

        var detail = result.Details.Should().ContainSingle().Subject;
        detail.AdditionLines.Should().HaveCount(2);
        detail.AdditionLines[^1].ServiceCode.Should().Be("610901");
        detail.AdditionLines[^1].Units.Should().Be(1_330);
        detail.TotalUnits.Should().Be(14_300 + 1_330);
    }

    [Fact]
    public void Percentage_rows_do_not_compound_each_other()
    {
        // order=1と2の両行とも同一の対象合計14,300へ独立に加算する（%行同士は複利にしない）。
        var masters = MastersWithPercentage(
            ("adj-pct-a", "610901", 0.093m, 1),
            ("adj-pct-b", "610902", 0.017m, 2));

        var result = ClaimCalculator.Calculate(masters, Request([CapabilityWpsI]));

        var detail = result.Details.Should().ContainSingle().Subject;
        // 14,300×0.093=1,329.9→1,330。14,300×0.017=243.1→243（複利なら243を超える）。
        detail.AdditionLines.Select(line => line.Units).Should().ContainInOrder(1_330, 243);
        detail.TotalUnits.Should().Be(14_300 + 1_330 + 243);
    }

    [Fact]
    public void Duplicate_percentage_calculation_orders_fail_closed()
    {
        var masters = MastersWithPercentage(
            ("adj-pct-a", "610901", 0.093m, 1),
            ("adj-pct-b", "610902", 0.017m, 1));

        var act = () => ClaimCalculator.Calculate(masters, Request([CapabilityWpsI]));

        act.Should().Throw<ClaimCalculationException>()
            .Which.Code.Should().Be(ClaimCalculationErrorCode.InvalidInput);
    }

    [Fact]
    public void Percentage_addition_without_a_rounding_rule_fails_closed()
    {
        var masters = MastersWithPercentage(("adj-pct-a", "610901", 0.093m, 1));
        var stripped = masters with
        {
            UnitAdjustments =
            [
                masters.UnitAdjustments[0],
                masters.UnitAdjustments[1] with { RoundingRuleId = null },
            ],
            ServiceCodes =
            [
                masters.ServiceCodes[0],
                masters.ServiceCodes[1],
                masters.ServiceCodes[2] with
                {
                    UnitRule = new UnitAdditionRule(
                        "adj-pct-a",
                        masters.UnitAdjustments[1].Amount,
                        "step-pct",
                        null,
                        BillingUnit.PerMonth),
                },
            ],
        };

        var act = () => ClaimCalculator.Calculate(stripped, Request([CapabilityWpsI]));

        act.Should().Throw<ClaimCalculationException>()
            .Which.Code.Should().Be(ClaimCalculationErrorCode.RoundingRuleUnavailable);
    }

    [Fact]
    public void Negative_addition_counts_are_rejected()
    {
        var request = new ClaimCalculationRequest(
            Month,
            Context([]),
            "region-x",
            "b-type",
            [new RecipientClaimSource(RecipientA, 20, 90, SyntheticCapYen, AbsenceSupportCount: -1)],
            Bindings());

        var act = () => ClaimCalculator.Calculate(Masters(), request);

        act.Should().Throw<ClaimCalculationException>()
            .Which.Code.Should().Be(ClaimCalculationErrorCode.InvalidInput);
    }

    private static ClaimSourceRef SourceRef() => new(
        "r6-fee-notice",
        "0000000000000000000000000000000000000000000000000000000000000",
        "synthetic",
        ClaimSourceEvidenceRole.Authoritative,
        [ClaimSourceSupport.MasterValues, ClaimSourceSupport.EffectivePeriod]);

    private static ClaimConditionDefinition Condition(
        string key, ClaimConditionKind kind, ClaimConditionOperand operand) => new(
        key, new ServiceMonth(2024, 4), null, kind, ClaimConditionOperator.Equals, operand, [SourceRef()]);

    private static UnitAdjustmentMasterRow Adjustment(
        string key, UnitAdjustmentAmount amount, BillingUnit billingUnit) => new(
        key, amount, "step-add", null, billingUnit,
        new ServiceMonth(2024, 4), null, [SourceRef()]);

    private static ServiceCodeMasterRow AdditionService(
        string key, string serviceCode, string officialLabel, string adjustmentKey,
        UnitAdjustmentAmount amount, BillingUnit billingUnit,
        IReadOnlyList<string> conditionSelectors) => new(
        key, serviceCode, officialLabel, "b-type", [$"selector:{key}"], conditionSelectors,
        new UnitAdditionRule(adjustmentKey, amount, "step-add", null, billingUnit),
        [new ClaimComponentRef(ClaimComponentMasterKind.Additions, adjustmentKey, ClaimComponentRole.Adjustment)],
        new ServiceMonth(2024, 4), null, [SourceRef()]);

    private static ClaimCalculationMasterBundle Masters()
    {
        var amountI = new UnitsPerCountAmount(15, "count-days");
        var amountII = new UnitsPerCountAmount(10, "count-days");
        return new ClaimCalculationMasterBundle(
            BasicRewards:
            [
                new BasicRewardMasterRow(
                    "base-a", "band-x", "staff-x", "cap-x", "610000", 700,
                    new ServiceMonth(2024, 4), null, [SourceRef()]),
            ],
            UnitAdjustments:
            [
                Adjustment("adj-a1", amountI, BillingUnit.PerDay),
                Adjustment("adj-a2", amountII, BillingUnit.PerDay),
            ],
            RegionUnitPrices:
            [
                new RegionUnitPriceMasterRow(
                    "price-x", "region-x", "b-type", 10.00m,
                    new ServiceMonth(2024, 4), null, [SourceRef()]),
            ],
            BurdenCaps: [],
            TransitionRules: [],
            ServiceCodes:
            [
                new ServiceCodeMasterRow(
                    "svc-base", "610000", "基本Ａ", "b-type", ["selector:svc-base"], ["cond-system-b"],
                    new BaseComponentPassThroughRule("base-a", "step-base", null, BillingUnit.PerDay),
                    [new ClaimComponentRef(ClaimComponentMasterKind.BasicRewards, "base-a", ClaimComponentRole.Base)],
                    new ServiceMonth(2024, 4), null, [SourceRef()]),
                AdditionService(
                    "svc-add-a1", "610101", "加算Ａ（Ⅰ）", "adj-a1", amountI, BillingUnit.PerDay,
                    ["cond-system-b", "cond-cap-a1"]),
                AdditionService(
                    "svc-add-a2", "610102", "加算Ａ（Ⅱ）", "adj-a2", amountII, BillingUnit.PerDay,
                    ["cond-system-b", "cond-cap-a2"]),
            ],
            ConditionDefinitions:
            [
                Condition("cond-system-b", ClaimConditionKind.RewardSystem, new ClaimConditionTokenOperand("b-type")),
                Condition(
                    "cond-cap-a1", ClaimConditionKind.OfficeCapability,
                    new ClaimConditionTokenOperand(CapabilityWpsI)),
                Condition(
                    "cond-cap-a2", ClaimConditionKind.OfficeCapability,
                    new ClaimConditionTokenOperand(CapabilityWpsII)),
            ]);
    }

    private static ClaimCalculationMasterBundle MastersWithFixedAdditions()
    {
        var masters = Masters();
        var perDay = new FixedUnitsAmount(30);
        var perMonth = new FixedUnitsAmount(55);
        return masters with
        {
            UnitAdjustments =
            [
                Adjustment("adj-fix-day", perDay, BillingUnit.PerDay),
                Adjustment("adj-fix-month", perMonth, BillingUnit.PerMonth),
            ],
            ServiceCodes =
            [
                masters.ServiceCodes[0],
                AdditionService(
                    "svc-fix-day", "610201", "加算Ｂ", "adj-fix-day", perDay, BillingUnit.PerDay,
                    ["cond-system-b", "cond-cap-a1"]),
                AdditionService(
                    "svc-fix-month", "610202", "加算Ｃ", "adj-fix-month", perMonth, BillingUnit.PerMonth,
                    ["cond-system-b", "cond-cap-a1"]),
            ],
        };
    }
}
