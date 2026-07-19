using FluentAssertions;
using Tsumugi.Domain.Logic.Claim;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Tests.Logic.Claim;

/// <summary>
/// Task 12（ADR 0022）: 利用者負担額＝min(1割相当額, 区分上限, 証上限, 上限額管理結果後額)の
/// 適用と、上限額管理結果区分・管理結果後額のペア入力検証を固定する。
/// 基本報酬・地域単価は<see cref="ClaimCalculatorTests"/>と同じ合成語彙（700単位/日・10.00円/単位）を
/// 再利用し、区分上限だけADR 0022の実値（一般1＝9,300円）を使ったgolden caseを1件含める
/// （数値の根拠はADR 0022本文・出典r6-disability-support-guide-202404物理9頁）。
/// </summary>
public sealed class ClaimCalculatorBurdenTests
{
    private static readonly ServiceMonth Month = new(2025, 4);
    private static readonly Guid RecipientA = Guid.Parse("44444444-4444-4444-4444-444444444444");

    // ADR 0022の一般1区分上限（出典: r6-disability-support-guide-202404 物理9頁）。
    // production seedの値と同一だが、Domainテストはproduction seedへ依存できないためここに
    // 再掲する（ClaimCalculatorGoldenCaseTestsの既存方針と同じ）。
    private const string General1BurdenCategory = "general-1";
    private const int General1CapYen = 9_300;

    [Fact]
    public void Golden_case_general1_category_cap_binds_before_statutory_burden()
    {
        // 700単位/日×20日=14,000単位。総費用額=14,000×10.00円=140,000円。
        // 1割相当額=140,000×10/100=14,000円。証上限は一般1の制度上限と同額の9,300円
        // （市町村認定が制度上限をそのまま採用したケース。ADR 0022手順4: 証上限≦区分上限）。
        // 1割相当額14,000円 > 区分上限9,300円 → 負担額は9,300円に制限される（ADR 0022手順5）。
        var result = ClaimCalculator.Calculate(Masters(), new ClaimCalculationRequest(
            Month, DefaultContext(), "region-a", "b-type",
            [new RecipientClaimSource(
                RecipientA, BilledDays: 20, BenefitRatePercent: 90,
                CertificateMonthlyCapYen: General1CapYen, BurdenCategory: General1BurdenCategory)]));

        var detail = result.Details.Should().ContainSingle().Subject;
        detail.TotalCostYen.Should().Be(140_000);
        detail.BurdenYen.Should().Be(General1CapYen);
        detail.BenefitYen.Should().Be(140_000 - General1CapYen);

        // 不変条件（ADR 0022）: 負担(9,300) ≦ 証記載上限(9,300) ≦ 法定(区分)上限(9,300)。
        detail.BurdenYen.Should().BeLessThanOrEqualTo(General1CapYen);
    }

    [Fact]
    public void Certificate_cap_binds_when_lower_than_the_category_cap()
    {
        // 証上限4,600円 < 区分上限9,300円 < 1割相当額14,000円 → 証上限が最も厳しく効く。
        // 不変条件 証上限(4,600) ≦ 区分上限(9,300) を満たす。
        var result = ClaimCalculator.Calculate(Masters(), new ClaimCalculationRequest(
            Month, DefaultContext(), "region-a", "b-type",
            [new RecipientClaimSource(
                RecipientA, BilledDays: 20, BenefitRatePercent: 90,
                CertificateMonthlyCapYen: 4_600, BurdenCategory: General1BurdenCategory)]));

        var detail = result.Details.Should().ContainSingle().Subject;
        detail.BurdenYen.Should().Be(4_600);
        detail.BurdenYen.Should().BeLessThanOrEqualTo(4_600);
        (4_600).Should().BeLessThanOrEqualTo(General1CapYen);
    }

    [Fact]
    public void Rejects_certificate_cap_that_exceeds_the_category_cap()
    {
        // ADR 0022手順4の不変条件: 証上限は区分上限以下でなければならない。10,000 > 9,300は
        // 市町村認定と入力の不整合であり、算定を停止する（区分上限へ丸めない）。
        var act = () => ClaimCalculator.Calculate(Masters(), new ClaimCalculationRequest(
            Month, DefaultContext(), "region-a", "b-type",
            [new RecipientClaimSource(
                RecipientA, BilledDays: 20, BenefitRatePercent: 90,
                CertificateMonthlyCapYen: 10_000, BurdenCategory: General1BurdenCategory)]));

        act.Should().Throw<ClaimCalculationException>()
            .Which.Code.Should().Be(ClaimCalculationErrorCode.InvalidInput);
    }

    [Fact]
    public void Upper_limit_managed_amount_binds_when_lower_than_the_provisional_burden()
    {
        // 上限額管理結果区分2（合算額が証上限以下で調整なし）で、管理結果後額5,000円が
        // 暫定負担額（証上限9,300円）よりも小さい場合、管理結果後額を最終負担額とする（手順9）。
        var result = ClaimCalculator.Calculate(Masters(), new ClaimCalculationRequest(
            Month, DefaultContext(), "region-a", "b-type",
            [new RecipientClaimSource(
                RecipientA, BilledDays: 20, BenefitRatePercent: 90,
                CertificateMonthlyCapYen: General1CapYen, BurdenCategory: General1BurdenCategory,
                UpperLimitResult: UpperLimitManagementResult.Result2,
                UpperLimitManagedAmountYen: 5_000)]));

        var detail = result.Details.Should().ContainSingle().Subject;
        detail.BurdenYen.Should().Be(5_000);
        // 不変条件チェーン: 負担(5,000) ≦ 証上限(9,300) ≦ 区分上限(9,300)。
        detail.BurdenYen.Should().BeLessThanOrEqualTo(General1CapYen);
    }

    [Fact]
    public void Upper_limit_management_not_applicable_leaves_the_provisional_burden_unchanged()
    {
        // 上限額管理対象外（UpperLimitResult=null）はそのまま暫定負担額を最終額とする。
        var result = ClaimCalculator.Calculate(Masters(), new ClaimCalculationRequest(
            Month, DefaultContext(), "region-a", "b-type",
            [new RecipientClaimSource(
                RecipientA, BilledDays: 20, BenefitRatePercent: 90,
                CertificateMonthlyCapYen: General1CapYen, BurdenCategory: General1BurdenCategory)]));

        result.Details.Should().ContainSingle().Which.BurdenYen.Should().Be(General1CapYen);
    }

    [Fact]
    public void Rejects_managed_amount_without_a_result_code()
        => FluentActions.Invoking(() => ClaimCalculator.Calculate(Masters(), new ClaimCalculationRequest(
                Month, DefaultContext(), "region-a", "b-type",
                [new RecipientClaimSource(
                    RecipientA, BilledDays: 20, BenefitRatePercent: 90,
                    CertificateMonthlyCapYen: General1CapYen, BurdenCategory: General1BurdenCategory,
                    UpperLimitResult: null,
                    UpperLimitManagedAmountYen: 5_000)])))
            .Should().Throw<ClaimCalculationException>()
            .Which.Code.Should().Be(ClaimCalculationErrorCode.InvalidInput);

    [Fact]
    public void Rejects_result_code_without_a_managed_amount()
        => FluentActions.Invoking(() => ClaimCalculator.Calculate(Masters(), new ClaimCalculationRequest(
                Month, DefaultContext(), "region-a", "b-type",
                [new RecipientClaimSource(
                    RecipientA, BilledDays: 20, BenefitRatePercent: 90,
                    CertificateMonthlyCapYen: General1CapYen, BurdenCategory: General1BurdenCategory,
                    UpperLimitResult: UpperLimitManagementResult.Result2,
                    UpperLimitManagedAmountYen: null)])))
            .Should().Throw<ClaimCalculationException>()
            .Which.Code.Should().Be(ClaimCalculationErrorCode.InvalidInput);

    [Fact]
    public void Rejects_negative_managed_amount()
        => FluentActions.Invoking(() => ClaimCalculator.Calculate(Masters(), new ClaimCalculationRequest(
                Month, DefaultContext(), "region-a", "b-type",
                [new RecipientClaimSource(
                    RecipientA, BilledDays: 20, BenefitRatePercent: 90,
                    CertificateMonthlyCapYen: General1CapYen, BurdenCategory: General1BurdenCategory,
                    UpperLimitResult: UpperLimitManagementResult.Result1,
                    UpperLimitManagedAmountYen: -1)])))
            .Should().Throw<ClaimCalculationException>()
            .Which.Code.Should().Be(ClaimCalculationErrorCode.InvalidInput);

    [Fact]
    public void Rejects_an_undefined_upper_limit_result_code()
        => FluentActions.Invoking(() => ClaimCalculator.Calculate(Masters(), new ClaimCalculationRequest(
                Month, DefaultContext(), "region-a", "b-type",
                [new RecipientClaimSource(
                    RecipientA, BilledDays: 20, BenefitRatePercent: 90,
                    CertificateMonthlyCapYen: General1CapYen, BurdenCategory: General1BurdenCategory,
                    UpperLimitResult: (UpperLimitManagementResult)99,
                    UpperLimitManagedAmountYen: 1_000)])))
            .Should().Throw<ClaimCalculationException>()
            .Which.Code.Should().Be(ClaimCalculationErrorCode.InvalidInput);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_blank_burden_category(string blankCategory)
        => FluentActions.Invoking(() => ClaimCalculator.Calculate(Masters(), new ClaimCalculationRequest(
                Month, DefaultContext(), "region-a", "b-type",
                [new RecipientClaimSource(
                    RecipientA, BilledDays: 20, BenefitRatePercent: 90,
                    CertificateMonthlyCapYen: 0, BurdenCategory: blankCategory)])))
            .Should().Throw<ClaimCalculationException>()
            .Which.Code.Should().Be(ClaimCalculationErrorCode.InvalidInput);

    [Fact]
    public void Fails_closed_when_no_burden_cap_row_matches_the_category()
        => FluentActions.Invoking(() => ClaimCalculator.Calculate(Masters(), new ClaimCalculationRequest(
                Month, DefaultContext(), "region-a", "b-type",
                [new RecipientClaimSource(
                    RecipientA, BilledDays: 20, BenefitRatePercent: 90,
                    CertificateMonthlyCapYen: 0, BurdenCategory: "unknown-category")])))
            .Should().Throw<ClaimCalculationException>()
            .Which.Code.Should().Be(ClaimCalculationErrorCode.BurdenCapUnavailable);

    [Fact]
    public void Fails_closed_when_duplicate_burden_cap_rows_match_the_category()
    {
        var duplicated = Masters() with
        {
            BurdenCaps =
            [
                BurdenCap(General1BurdenCategory, General1CapYen),
                BurdenCap(General1BurdenCategory, General1CapYen),
            ],
        };

        var act = () => ClaimCalculator.Calculate(duplicated, new ClaimCalculationRequest(
            Month, DefaultContext(), "region-a", "b-type",
            [new RecipientClaimSource(
                RecipientA, BilledDays: 20, BenefitRatePercent: 90,
                CertificateMonthlyCapYen: 0, BurdenCategory: General1BurdenCategory)]));

        act.Should().Throw<ClaimCalculationException>()
            .Which.Code.Should().Be(ClaimCalculationErrorCode.BurdenCapUnavailable);
    }

    private static ClaimBillingConditionContext DefaultContext() => new(
        RewardSystem: "b-type",
        PaymentBand: "band-a",
        CapacityHeadcount: 20,
        StaffingKey: "staff-a",
        AverageWageBandOption: new AverageWageBandOption(AverageWageBandOptionKind.Numeric, 3),
        R8ReformStatus: R8ReformStatus.NotApplicableBeforeR8);

    private static ClaimSourceRef SourceRef() => new(
        "doc-1",
        "0000000000000000000000000000000000000000000000000000000000000",
        "loc",
        ClaimSourceEvidenceRole.Authoritative,
        [ClaimSourceSupport.Conditions, ClaimSourceSupport.EffectivePeriod]);

    private static BasicRewardMasterRow BasicReward() => new(
        "base-a", "band-a", "staff-a", "cap-a", "610000", 700,
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

    private static ServiceCodeMasterRow ServiceCode() => new(
        "sc-a",
        "610000",
        "B型基本(合成)",
        "b-type",
        [],
        ["cond-system-b", "cond-band-a", "cond-cap-a", "cond-staff-a"],
        new BaseComponentPassThroughRule("base-a", "step-base", null, BillingUnit.PerDay),
        [new ClaimComponentRef(ClaimComponentMasterKind.BasicRewards, "base-a", ClaimComponentRole.Base)],
        new ServiceMonth(2024, 4),
        null,
        [SourceRef()]);

    private static RegionUnitPriceMasterRow RegionUnitPrice() => new(
        "price-a", "region-a", "b-type", 10.00m, new ServiceMonth(2024, 4), null, [SourceRef()]);

    private static BurdenCapMasterRow BurdenCap(string burdenCategory, int capYen) => new(
        $"burden-cap-{burdenCategory}", burdenCategory, capYen, new ServiceMonth(2024, 4), null, [SourceRef()]);

    private static ClaimCalculationMasterBundle Masters() => new(
        BasicRewards: [BasicReward()],
        UnitAdjustments: [],
        RegionUnitPrices: [RegionUnitPrice()],
        BurdenCaps: [BurdenCap(General1BurdenCategory, General1CapYen)],
        TransitionRules: [],
        ServiceCodes: [ServiceCode()],
        ConditionDefinitions: DefaultConditions());
}
