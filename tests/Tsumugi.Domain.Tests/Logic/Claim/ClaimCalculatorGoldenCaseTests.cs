using FluentAssertions;
using Tsumugi.Domain.Logic.Claim;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Tests.Logic.Claim;

/// <summary>
/// ADR 0027 §4「手計算検証ケース（golden case期待値）」の3ケースをそのまま再現する。
/// マスタ行はADR 0027 §2.1〜2.3・§3の該当行の値をこのテストファイル内に再掲する
/// （DomainテストはInfrastructureのseedへ依存できないため）。3ケースとも法31条特例は不適用、
/// 受給者証上限・制度上限は1割相当額以上、上限額管理の対象外、加算・減算なし（基本報酬のみ）という
/// ADR 0027 §4の前提に合わせ、<see cref="RecipientClaimSource.CertificateMonthlyCapYen"/>はnull
/// （証上限の指定なし＝1割相当額をそのまま利用者負担とする）で表す。
/// </summary>
public sealed class ClaimCalculatorGoldenCaseTests
{
    private static readonly ServiceMonth Month = new(2025, 4);
    private static readonly Guid RecipientA = Guid.Parse("22222222-2222-2222-2222-222222222222");

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
                R8ReformStatus: R8ReformStatus.NotApplicableBeforeR8),
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
                R8ReformStatus: R8ReformStatus.NotApplicableBeforeR8),
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
                R8ReformStatus: R8ReformStatus.NotApplicableBeforeR8),
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
            [new RecipientClaimSource(RecipientA, billedDays, BenefitRatePercent: 90, CertificateMonthlyCapYen: null)]));

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

    private static ServiceCodeMasterRow ServiceCode(
        string key, string serviceCode, string officialLabel, IReadOnlyList<string> conditionSelectors,
        string baseComponentKey) => new(
        key, serviceCode, officialLabel, "b-type", [], conditionSelectors,
        new BaseComponentPassThroughRule(baseComponentKey, "step-base", null, BillingUnit.PerDay),
        [new ClaimComponentRef(ClaimComponentMasterKind.BasicRewards, baseComponentKey, ClaimComponentRole.Base)],
        new ServiceMonth(2024, 4), null, [SourceRef()]);

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
        UnitAdjustments: [],
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
        ],
        ConditionDefinitions:
        [
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
