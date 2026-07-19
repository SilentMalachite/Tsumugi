using FluentAssertions;
using Tsumugi.Domain.Logic.Claim;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Tests.Logic.Claim;

/// <summary>
/// ADR 0027 §4「手計算検証ケース（golden case期待値）」の3ケースと、ADR 0028決定6の
/// 加算つき3ケース（A/B/C）を再現する。マスタ行はADR 0027 §2.1〜2.3・§3およびADR 0028決定2〜4の
/// 該当行の値をこのテストファイル内に再掲する（DomainテストはInfrastructureのseedへ依存できない
/// ため）。全ケースとも法31条特例は不適用、受給者証上限・制度上限は1割相当額以上、上限額管理の
/// 対象外という前提に合わせ、<see cref="RecipientClaimSource.CertificateMonthlyCapYen"/>には
/// <see cref="UnboundedSyntheticCapYen"/>（テスト専用の合成上限。1割相当額を制限しない＝そのまま
/// 利用者負担とする）を渡す。ADR 0027の3ケースは体制届キー空集合（加算体制なし）で加算行が
/// 選ばれず、期待値は加算マスタ追加の前後で完全に同一である。
/// </summary>
public sealed class ClaimCalculatorGoldenCaseTests
{
    private static readonly ServiceMonth Month = new(2025, 4);
    private static readonly Guid RecipientA = Guid.Parse("22222222-2222-2222-2222-222222222222");

    // テスト専用の合成上限（制度上の値ではない）。CertificateMonthlyCapYenは必須intのため、
    // golden caseの前提（証上限は1割相当額以上）を「十分大きい合成上限」で表す。
    private const int UnboundedSyntheticCapYen = 9_999_999;

    public static TheoryData<string, ClaimBillingConditionContext, string, int, int, int, int, int> GoldenCases()
    {
        var data = new TheoryData<string, ClaimBillingConditionContext, string, int, int, int, int, int>();

        // ケース1（ADR 0027 §4）: cap-20-or-less × band-20000-25000 × staff-7.5-1 × 22日 × region-grade-2
        // サービスコード462049（就継ＢⅡ１５、ADR 0027 §2.2）＝637単位/日。
        // 月次給付単位数: 637×22=14,014単位。総費用額: 14,014×10.91円=152,892.74円→152,892円。
        // 1割相当額: 152,892×10/100=15,289.2円→15,289円。給付費: 152,892−15,289=137,603円。
        data.Add(
            "case1-462049",
            new ClaimBillingConditionContext(
                RewardSystem: "b-type",
                PaymentBand: "band-20000-25000",
                CapacityHeadcount: 20,
                StaffingKey: "staff-7.5-1",
                AverageWageBandOption: new AverageWageBandOption(AverageWageBandOptionKind.Numeric, 5),
                R8ReformStatus: R8ReformStatus.NotApplicableBeforeR8,
                OfficeCapabilityKeys: []),
            "region-grade-2",
            22,
            14014,
            152892,
            15289,
            137603);

        // ケース2（ADR 0027 §4）: cap-20-or-less × band-under-10000 × staff-10-1 × 20日 × region-other
        // サービスコード462883（就継ＢⅢ１８、ADR 0027 §2.3）＝490単位/日。
        // 月次給付単位数: 490×20=9,800単位。総費用額: 9,800×10.00円=98,000.00円→98,000円。
        // 1割相当額: 98,000×10/100=9,800.0円→9,800円。給付費: 98,000−9,800=88,200円。
        data.Add(
            "case2-462883",
            new ClaimBillingConditionContext(
                RewardSystem: "b-type",
                PaymentBand: "band-under-10000",
                CapacityHeadcount: 15,
                StaffingKey: "staff-10-1",
                AverageWageBandOption: new AverageWageBandOption(AverageWageBandOptionKind.Numeric, 8),
                R8ReformStatus: R8ReformStatus.NotApplicableBeforeR8,
                OfficeCapabilityKeys: []),
            "region-other",
            20,
            9800,
            98000,
            9800,
            88200);

        // ケース3（ADR 0027 §4）: cap-21-40 × band-45000-plus × staff-6-1 × 23日 × region-grade-1
        // サービスコード463028（就継ＢⅠ２１、ADR 0027 §2.1）＝746単位/日。
        // 月次給付単位数: 746×23=17,158単位。総費用額: 17,158×11.14円=191,140.12円→191,140円。
        // 1割相当額: 191,140×10/100=19,114.0円→19,114円。給付費: 191,140−19,114=172,026円。
        data.Add(
            "case3-463028",
            new ClaimBillingConditionContext(
                RewardSystem: "b-type",
                PaymentBand: "band-45000-plus",
                CapacityHeadcount: 30,
                StaffingKey: "staff-6-1",
                AverageWageBandOption: new AverageWageBandOption(AverageWageBandOptionKind.Numeric, 1),
                R8ReformStatus: R8ReformStatus.NotApplicableBeforeR8,
                OfficeCapabilityKeys: []),
            "region-grade-1",
            23,
            17158,
            191140,
            19114,
            172026);

        return data;
    }

    [Theory]
    [MemberData(nameof(GoldenCases))]
    public void Matches_adr_0027_worked_examples(
        string caseId,
        ClaimBillingConditionContext context,
        string regionKey,
        int billedDays,
        int expectedUnits,
        int expectedCostYen,
        int expectedBurdenYen,
        int expectedBenefitYen)
    {
        var result = ClaimCalculator.Calculate(Masters(), new ClaimCalculationRequest(
            Month, context, regionKey, "b-type",
            [new RecipientClaimSource(RecipientA, billedDays, BenefitRatePercent: 90, CertificateMonthlyCapYen: UnboundedSyntheticCapYen)],
            CountSelectorBindings));

        var detail = result.Details.Should().ContainSingle().Subject;
        detail.TotalUnits.Should().Be(expectedUnits, because: caseId);
        detail.TotalCostYen.Should().Be(expectedCostYen, because: caseId);
        detail.BurdenYen.Should().Be(expectedBurdenYen, because: caseId);
        detail.BenefitYen.Should().Be(expectedBenefitYen, because: caseId);
        result.TotalUnits.Should().Be(expectedUnits, because: caseId);
        result.TotalCostYen.Should().Be(expectedCostYen, because: caseId);
        result.TotalBurdenYen.Should().Be(expectedBurdenYen, because: caseId);
        result.TotalBenefitYen.Should().Be(expectedBenefitYen, because: caseId);
    }

    /// <summary>
    /// ADR 0028決定6 ケースA: ADR 0027ケース1＋固定単位加算4種
    /// （cap-20-or-less × band-20000-25000 × staff-7.5-1 × region-grade-2、2025-04）。
    /// 基本 637×22=14,014 ＋ 福祉専門職員(Ⅰ) 15×22=330 ＋ 食事提供 30×20=600 ＋
    /// 送迎(Ⅰ) 21×40=840 ＋ 欠席時対応 94×2=188 ＝ 15,972単位。
    /// 総費用額 15,972×10.91=174,254.52→174,254円、1割相当 17,425円、給付費 156,829円。
    /// </summary>
    [Fact]
    public void Matches_adr_0028_worked_example_a_fixed_unit_additions()
    {
        var context = new ClaimBillingConditionContext(
            RewardSystem: "b-type",
            PaymentBand: "band-20000-25000",
            CapacityHeadcount: 20,
            StaffingKey: "staff-7.5-1",
            AverageWageBandOption: new AverageWageBandOption(AverageWageBandOptionKind.Numeric, 5),
            R8ReformStatus: R8ReformStatus.NotApplicableBeforeR8,
            OfficeCapabilityKeys:
            [
                "mhlw.b46.capability.welfare-professional-staffing.3",
                "mhlw.b46.capability.meal-provision-system.2",
                "mhlw.b46.capability.transport-system.3",
            ]);

        var result = ClaimCalculator.Calculate(Masters(), new ClaimCalculationRequest(
            Month, context, "region-grade-2", "b-type",
            [
                new RecipientClaimSource(
                    RecipientA, BilledDays: 22, BenefitRatePercent: 90,
                    CertificateMonthlyCapYen: UnboundedSyntheticCapYen,
                    AbsenceSupportCount: 2,
                    MealProvidedDays: 20,
                    TransportOneWayCount: 40),
            ],
            CountSelectorBindings));

        var detail = result.Details.Should().ContainSingle().Subject;
        detail.AdditionLines.Select(line => (line.ServiceCode, line.Units)).Should().BeEquivalentTo(
        [
            ("466037", 330),
            ("465070", 600),
            ("466590", 840),
            ("466040", 188),
        ]);
        detail.TotalUnits.Should().Be(15_972);
        detail.TotalCostYen.Should().Be(174_254);
        detail.BurdenYen.Should().Be(17_425);
        detail.BenefitYen.Should().Be(156_829);
    }

    /// <summary>
    /// ADR 0028決定6 ケースB: ケースA＋統一 福祉・介護職員等処遇改善加算(Ⅰ)（2024-06以降の月）。
    /// 月次対象単位合計（target.b46.items-1-to-16-4.v1）= 15,972（%行自身は含まない）。
    /// 処遇改善(Ⅰ) 15,972×93/1000=1,485.396 → claim.rounding.units.half-up.v1 → 1,485単位。
    /// 最終給付単位数 17,457。総費用額 17,457×10.91=190,455.87→190,455円、
    /// 1割相当 19,045円、給付費 171,410円。
    /// </summary>
    [Fact]
    public void Matches_adr_0028_worked_example_b_unified_treatment_improvement()
    {
        var context = new ClaimBillingConditionContext(
            RewardSystem: "b-type",
            PaymentBand: "band-20000-25000",
            CapacityHeadcount: 20,
            StaffingKey: "staff-7.5-1",
            AverageWageBandOption: new AverageWageBandOption(AverageWageBandOptionKind.Numeric, 5),
            R8ReformStatus: R8ReformStatus.NotApplicableBeforeR8,
            OfficeCapabilityKeys:
            [
                "mhlw.b46.capability.welfare-professional-staffing.3",
                "mhlw.b46.capability.meal-provision-system.2",
                "mhlw.b46.capability.transport-system.3",
                "mhlw.b46.capability.treatment-improvement.2",
            ]);

        var result = ClaimCalculator.Calculate(Masters(), new ClaimCalculationRequest(
            Month, context, "region-grade-2", "b-type",
            [
                new RecipientClaimSource(
                    RecipientA, BilledDays: 22, BenefitRatePercent: 90,
                    CertificateMonthlyCapYen: UnboundedSyntheticCapYen,
                    AbsenceSupportCount: 2,
                    MealProvidedDays: 20,
                    TransportOneWayCount: 40),
            ],
            CountSelectorBindings));

        var detail = result.Details.Should().ContainSingle().Subject;
        detail.AdditionLines.Select(line => (line.ServiceCode, line.Units)).Should().BeEquivalentTo(
        [
            ("466037", 330),
            ("465070", 600),
            ("466590", 840),
            ("466040", 188),
            ("465120", 1_485),
        ]);
        detail.TotalUnits.Should().Be(17_457);
        detail.TotalCostYen.Should().Be(190_455);
        detail.BurdenYen.Should().Be(19_045);
        detail.BenefitYen.Should().Be(171_410);
    }

    /// <summary>
    /// ADR 0028決定6 ケースC: ADR 0027ケース2＋定員連動・初期・同一敷地送迎
    /// （cap-20-or-less × band-under-10000 × staff-10-1 × region-other、2025-04）。
    /// 基本 490×20=9,800 ＋ 目標工賃達成指導員（定員20人以下）45×20=900 ＋ 初期 30×10=300 ＋
    /// 送迎(Ⅱ)同一敷地 7×36=252 ＝ 11,252単位。
    /// 総費用額 11,252×10.00=112,520円、1割相当 11,252円、給付費 101,268円。
    /// </summary>
    [Fact]
    public void Matches_adr_0028_worked_example_c_capacity_initial_and_same_premises()
    {
        var context = new ClaimBillingConditionContext(
            RewardSystem: "b-type",
            PaymentBand: "band-under-10000",
            CapacityHeadcount: 15,
            StaffingKey: "staff-10-1",
            AverageWageBandOption: new AverageWageBandOption(AverageWageBandOptionKind.Numeric, 8),
            R8ReformStatus: R8ReformStatus.NotApplicableBeforeR8,
            OfficeCapabilityKeys:
            [
                "mhlw.b46.capability.target-wage-instructor.2",
                "mhlw.b46.capability.transport-system.4",
            ]);

        var result = ClaimCalculator.Calculate(Masters(), new ClaimCalculationRequest(
            Month, context, "region-other", "b-type",
            [
                new RecipientClaimSource(
                    RecipientA, BilledDays: 20, BenefitRatePercent: 90,
                    CertificateMonthlyCapYen: UnboundedSyntheticCapYen,
                    InitialPeriodServiceDays: 10,
                    TransportSamePremisesOneWayCount: 36),
            ],
            CountSelectorBindings));

        var detail = result.Details.Should().ContainSingle().Subject;
        detail.AdditionLines.Select(line => (line.ServiceCode, line.Units)).Should().BeEquivalentTo(
        [
            ("465255", 900),
            ("465050", 300),
            ("466593", 252),
        ]);
        detail.TotalUnits.Should().Be(11_252);
        detail.TotalCostYen.Should().Be(112_520);
        detail.BurdenYen.Should().Be(11_252);
        detail.BenefitYen.Should().Be(101_268);
    }

    /// <summary>ADR 0028決定2のcountSelector正準トークン束縛（productionと同一語彙）。</summary>
    private static readonly IReadOnlyDictionary<string, ClaimCountMetric> CountSelectorBindings =
        new Dictionary<string, ClaimCountMetric>(StringComparer.Ordinal)
        {
            ["count.b46.service-days.v1"] = ClaimCountMetric.ServiceDays,
            ["count.b46.initial-period-service-days.v1"] = ClaimCountMetric.InitialPeriodServiceDays,
            ["count.b46.absence-support.v1"] = ClaimCountMetric.AbsenceSupport,
            ["count.b46.meal-provided-days.v1"] = ClaimCountMetric.MealProvidedDays,
            ["count.b46.transport-one-way.v1"] = ClaimCountMetric.TransportOneWay,
            ["count.b46.transport-one-way.same-premises.v1"] = ClaimCountMetric.TransportOneWaySamePremises,
        };

    private static ClaimSourceRef SourceRef() => new(
        "r6-fee-notice",
        "0000000000000000000000000000000000000000000000000000000000000",
        "ADR 0027 §2.1〜2.3・§3",
        ClaimSourceEvidenceRole.Authoritative,
        [ClaimSourceSupport.MasterValues, ClaimSourceSupport.EffectivePeriod]);

    private static ClaimConditionDefinition ConditionDefinition(
        string key, ClaimConditionKind kind, ClaimConditionOperator @operator, ClaimConditionOperand operand) => new(
        key, new ServiceMonth(2024, 4), null, kind, @operator, operand, [SourceRef()]);

    private static BasicRewardMasterRow BasicReward(
        string key, string paymentBand, string staffingKey, string capacityKey, string serviceCode, int baseUnits) => new(
        key, paymentBand, staffingKey, capacityKey, serviceCode, baseUnits,
        new ServiceMonth(2024, 4), null, [SourceRef()]);

    /// <summary>
    /// 月次対象合計のtargetSelector（ADR 0028決定2）。基本報酬＋固定単位加算の行のSelectorsに
    /// 付与し、割合行（%行自身は含まない）がこのトークンで対象行を選ぶ。
    /// </summary>
    private const string MonthlyTargetSelector = "target.b46.items-1-to-16-4.v1";

    private static ServiceCodeMasterRow ServiceCode(
        string key, string serviceCode, string officialLabel, IReadOnlyList<string> conditionSelectors,
        string baseComponentKey) => new(
        key, serviceCode, officialLabel, "b-type", [MonthlyTargetSelector], conditionSelectors,
        new BaseComponentPassThroughRule(baseComponentKey, "step-base", null, BillingUnit.PerDay),
        [new ClaimComponentRef(ClaimComponentMasterKind.BasicRewards, baseComponentKey, ClaimComponentRole.Base)],
        new ServiceMonth(2024, 4), null, [SourceRef()]);

    private static UnitAdjustmentMasterRow Adjustment(
        string key, UnitAdjustmentAmount amount, BillingUnit billingUnit) => new(
        key, amount, "step-add", null, billingUnit, new ServiceMonth(2024, 4), null, [SourceRef()]);

    private static ServiceCodeMasterRow AdditionService(
        string key, string serviceCode, string officialLabel, string adjustmentKey,
        UnitAdjustmentAmount amount, BillingUnit billingUnit,
        IReadOnlyList<string> conditionSelectors) => new(
        key, serviceCode, officialLabel, "b-type", [MonthlyTargetSelector], conditionSelectors,
        new UnitAdditionRule(adjustmentKey, amount, "step-add", null, billingUnit),
        [new ClaimComponentRef(ClaimComponentMasterKind.Additions, adjustmentKey, ClaimComponentRole.Adjustment)],
        new ServiceMonth(2024, 4), null, [SourceRef()]);

    // ADR 0028決定3の固定単位加算（golden case A/Cで使う行のみ再掲）。
    private static readonly UnitsPerCountAmount WelfareProfessionalI =
        new(15, "count.b46.service-days.v1");

    private static readonly UnitsPerCountAmount MealProvision =
        new(30, "count.b46.meal-provided-days.v1");

    private static readonly UnitsPerCountAmount TransportI =
        new(21, "count.b46.transport-one-way.v1");

    private static readonly UnitsPerCountAmount TransportIISamePremises =
        new(7, "count.b46.transport-one-way.same-premises.v1");

    private static readonly UnitsPerCountAmount AbsenceResponse =
        new(94, "count.b46.absence-support.v1", MonthlyCountCap: 4);

    private static readonly UnitsPerCountAmount TargetWageInstructorCap20 =
        new(45, "count.b46.service-days.v1");

    private static readonly UnitsPerCountAmount InitialAddition =
        new(30, "count.b46.initial-period-service-days.v1");

    // ADR 0028決定4.1: 統一 福祉・介護職員等処遇改善加算(Ⅰ)（2024-06-01〜2026-05-31、率93/1000）。
    private static readonly PercentageOfTargetAmount UnifiedTreatmentImprovementI = new(
        0.093m,
        PercentageApplicationKind.Add,
        PercentageBaseScope.MonthlyTargetUnitSum,
        MonthlyTargetSelector,
        CalculationOrder: 1);

    private static RegionUnitPriceMasterRow RegionUnitPrice(string regionKey, decimal unitPriceYen) => new(
        $"price-{regionKey}", regionKey, "b-type", unitPriceYen, new ServiceMonth(2024, 4), null, [SourceRef()]);

    private static ClaimCalculationMasterBundle Masters() => new(
        BasicRewards:
        [
            // ADR 0027 §2.2: cap-20-or-less × band-20000-25000 × staff-7.5-1 = 637単位/日、462049。
            BasicReward("base-462049", "band-20000-25000", "staff-7.5-1", "cap-20-or-less", "462049", 637),
            // ADR 0027 §2.3: cap-20-or-less × band-under-10000 × staff-10-1 = 490単位/日、462883。
            BasicReward("base-462883", "band-under-10000", "staff-10-1", "cap-20-or-less", "462883", 490),
            // ADR 0027 §2.1: cap-21-40 × band-45000-plus × staff-6-1 = 746単位/日、463028。
            BasicReward("base-463028", "band-45000-plus", "staff-6-1", "cap-21-40", "463028", 746),
        ],
        UnitAdjustments:
        [
            Adjustment("addition.welfare-professional-staffing.i", WelfareProfessionalI, BillingUnit.PerDay),
            Adjustment("addition.meal-provision", MealProvision, BillingUnit.PerDay),
            Adjustment("addition.transport.i", TransportI, BillingUnit.PerUse),
            Adjustment("addition.transport.ii-same-premises", TransportIISamePremises, BillingUnit.PerUse),
            Adjustment("addition.absence-response", AbsenceResponse, BillingUnit.PerUse),
            Adjustment("addition.target-wage-instructor.cap-20-or-less", TargetWageInstructorCap20, BillingUnit.PerDay),
            Adjustment("addition.initial", InitialAddition, BillingUnit.PerDay),
            new UnitAdjustmentMasterRow(
                "addition.treatment-improvement.unified.i",
                UnifiedTreatmentImprovementI,
                "claim.step.units.monthly-target.percentage.v1",
                "claim.rounding.units.half-up.v1",
                BillingUnit.PerMonth,
                new ServiceMonth(2024, 6),
                new ServiceMonth(2026, 5),
                [SourceRef()]),
        ],
        RegionUnitPrices:
        [
            // ADR 0027 §3: 二級地=10.91円、その他=10.00円、一級地=11.14円。
            RegionUnitPrice("region-grade-2", 10.91m),
            RegionUnitPrice("region-other", 10.00m),
            RegionUnitPrice("region-grade-1", 11.14m),
        ],
        BurdenCaps: [],
        TransitionRules: [],
        ServiceCodes:
        [
            ServiceCode(
                "sc-462049", "462049", "就継ＢⅡ１５",
                ["cond-system-b", "cond-band-20000-25000", "cond-cap-20-or-less", "cond-staff-7-5-1"],
                "base-462049"),
            ServiceCode(
                "sc-462883", "462883", "就継ＢⅢ１８",
                ["cond-system-b", "cond-band-under-10000", "cond-cap-20-or-less", "cond-staff-10-1"],
                "base-462883"),
            ServiceCode(
                "sc-463028", "463028", "就継ＢⅠ２１",
                ["cond-system-b", "cond-band-45000-plus", "cond-cap-21-40-min", "cond-cap-21-40-max", "cond-staff-6-1"],
                "base-463028"),
            // ADR 0028決定3の加算行（A/Cで検証する7行）。
            AdditionService(
                "sc-466037", "466037", "福祉専門職員配置等加算(Ⅰ)",
                "addition.welfare-professional-staffing.i", WelfareProfessionalI, BillingUnit.PerDay,
                ["cond-system-b", "cond-cap-wps-i"]),
            AdditionService(
                "sc-465070", "465070", "食事提供体制加算",
                "addition.meal-provision", MealProvision, BillingUnit.PerDay,
                ["cond-system-b", "cond-cap-meal"]),
            AdditionService(
                "sc-466590", "466590", "送迎加算(Ⅰ)",
                "addition.transport.i", TransportI, BillingUnit.PerUse,
                ["cond-system-b", "cond-cap-transport-i"]),
            AdditionService(
                "sc-466593", "466593", "送迎加算(Ⅱ)（同一敷地内）",
                "addition.transport.ii-same-premises", TransportIISamePremises, BillingUnit.PerUse,
                ["cond-system-b", "cond-cap-transport-ii"]),
            AdditionService(
                "sc-466040", "466040", "欠席時対応加算",
                "addition.absence-response", AbsenceResponse, BillingUnit.PerUse,
                ["cond-system-b"]),
            AdditionService(
                "sc-465255", "465255", "目標工賃達成指導員配置加算（定員20人以下）",
                "addition.target-wage-instructor.cap-20-or-less", TargetWageInstructorCap20, BillingUnit.PerDay,
                ["cond-system-b", "cond-cap-twi", "cond-cap-20-or-less"]),
            AdditionService(
                "sc-465050", "465050", "初期加算",
                "addition.initial", InitialAddition, BillingUnit.PerDay,
                ["cond-system-b"]),
            // %行はSelectorsにMonthlyTargetSelectorを載せない（%行自身は対象合計に含まない）。
            new ServiceCodeMasterRow(
                "sc-465120", "465120", "福祉・介護職員等処遇改善加算(Ⅰ)", "b-type",
                ["selector:sc-465120"],
                ["cond-system-b", "cond-cap-treatment-i"],
                new UnitAdditionRule(
                    "addition.treatment-improvement.unified.i",
                    UnifiedTreatmentImprovementI,
                    "claim.step.units.monthly-target.percentage.v1",
                    "claim.rounding.units.half-up.v1",
                    BillingUnit.PerMonth),
                [
                    new ClaimComponentRef(
                        ClaimComponentMasterKind.Additions,
                        "addition.treatment-improvement.unified.i",
                        ClaimComponentRole.Adjustment),
                ],
                new ServiceMonth(2024, 6),
                new ServiceMonth(2026, 5),
                [SourceRef()]),
        ],
        ConditionDefinitions:
        [
            ConditionDefinition(
                "cond-cap-wps-i", ClaimConditionKind.OfficeCapability, ClaimConditionOperator.Equals,
                new ClaimConditionTokenOperand("mhlw.b46.capability.welfare-professional-staffing.3")),
            ConditionDefinition(
                "cond-cap-meal", ClaimConditionKind.OfficeCapability, ClaimConditionOperator.Equals,
                new ClaimConditionTokenOperand("mhlw.b46.capability.meal-provision-system.2")),
            ConditionDefinition(
                "cond-cap-transport-i", ClaimConditionKind.OfficeCapability, ClaimConditionOperator.Equals,
                new ClaimConditionTokenOperand("mhlw.b46.capability.transport-system.3")),
            ConditionDefinition(
                "cond-cap-transport-ii", ClaimConditionKind.OfficeCapability, ClaimConditionOperator.Equals,
                new ClaimConditionTokenOperand("mhlw.b46.capability.transport-system.4")),
            ConditionDefinition(
                "cond-cap-twi", ClaimConditionKind.OfficeCapability, ClaimConditionOperator.Equals,
                new ClaimConditionTokenOperand("mhlw.b46.capability.target-wage-instructor.2")),
            ConditionDefinition(
                "cond-cap-treatment-i", ClaimConditionKind.OfficeCapability, ClaimConditionOperator.Equals,
                new ClaimConditionTokenOperand("mhlw.b46.capability.treatment-improvement.2")),
            ConditionDefinition(
                "cond-system-b", ClaimConditionKind.RewardSystem, ClaimConditionOperator.Equals,
                new ClaimConditionTokenOperand("b-type")),
            ConditionDefinition(
                "cond-band-20000-25000", ClaimConditionKind.PaymentBand, ClaimConditionOperator.Equals,
                new ClaimConditionTokenOperand("band-20000-25000")),
            ConditionDefinition(
                "cond-band-under-10000", ClaimConditionKind.PaymentBand, ClaimConditionOperator.Equals,
                new ClaimConditionTokenOperand("band-under-10000")),
            ConditionDefinition(
                "cond-band-45000-plus", ClaimConditionKind.PaymentBand, ClaimConditionOperator.Equals,
                new ClaimConditionTokenOperand("band-45000-plus")),
            ConditionDefinition(
                "cond-cap-20-or-less", ClaimConditionKind.Capacity, ClaimConditionOperator.LessThanOrEqual,
                new ClaimConditionIntegerOperand(20)),
            ConditionDefinition(
                "cond-cap-21-40-min", ClaimConditionKind.Capacity, ClaimConditionOperator.GreaterThanOrEqual,
                new ClaimConditionIntegerOperand(21)),
            ConditionDefinition(
                "cond-cap-21-40-max", ClaimConditionKind.Capacity, ClaimConditionOperator.LessThanOrEqual,
                new ClaimConditionIntegerOperand(40)),
            ConditionDefinition(
                "cond-staff-7-5-1", ClaimConditionKind.Staffing, ClaimConditionOperator.Equals,
                new ClaimConditionTokenOperand("staff-7.5-1")),
            ConditionDefinition(
                "cond-staff-10-1", ClaimConditionKind.Staffing, ClaimConditionOperator.Equals,
                new ClaimConditionTokenOperand("staff-10-1")),
            ConditionDefinition(
                "cond-staff-6-1", ClaimConditionKind.Staffing, ClaimConditionOperator.Equals,
                new ClaimConditionTokenOperand("staff-6-1")),
        ]);
}
