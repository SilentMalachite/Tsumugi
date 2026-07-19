using FluentAssertions;
using Tsumugi.Domain.Logic.Claim;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Tests.Logic.Claim;

public sealed class ClaimCalculatorTests
{
    private static readonly ServiceMonth Month = new(2025, 4);
    private static readonly Guid RecipientA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid RecipientB = Guid.Parse("33333333-3333-3333-3333-333333333333");

    // テスト専用の合成上限（制度上の値ではない）。証上限が負担を制限しないことを表すため、
    // 起こりうる負担額より十分大きい値を用いる。CertificateMonthlyCapYenは必須intであり、
    // 「証上限の指定なし」をnullで表すことは許されない（ADR 0025のfail-closed方針）。
    private const int UnboundedSyntheticCapYen = 9_999_999;

    // テスト専用の合成負担区分・区分上限（制度上の値ではない）。ADR 0022の不変条件
    // （証上限≦区分上限）を壊さないよう、UnboundedSyntheticCapYen以上の区分上限を用意する。
    private const string SyntheticBurdenCategory = "cat-a";
    private const int SyntheticBurdenCategoryCapYen = 99_999_999;

    // 合成マスタは定員を頭数(int)で表す閾値条件（capacity, less-than-or-equal, 20）で表現する。
    // ServiceCodeResolverTestsと同じヘルパ形式（baseUnits=700, unitPriceYen=10.00m）を使う。
    private static readonly string[] DefaultConditionSelectors =
        ["cond-system-b", "cond-band-a", "cond-cap-a", "cond-staff-a"];

    [Fact]
    public void Calculates_basic_reward_only_recipient()
    {
        var result = ClaimCalculator.Calculate(SyntheticMasters(), new ClaimCalculationRequest(
            Month, DefaultContext(), "region-a", "b-type",
            [new RecipientClaimSource(RecipientA, BilledDays: 20, BenefitRatePercent: 90, CertificateMonthlyCapYen: 0, BurdenCategory: SyntheticBurdenCategory)]));

        var detail = result.Details.Should().ContainSingle().Subject;
        detail.TotalUnits.Should().Be(700 * 20);
        detail.TotalCostYen.Should().Be(140000); // 700*20*10.00 = 140,000.00 → 円未満切捨て（端数なし）
        detail.BurdenYen.Should().Be(0); // cap=0（生活保護等）→ 負担0
        detail.BenefitYen.Should().Be(140000); // 総費用額 − 負担額
        result.TotalUnits.Should().Be(detail.TotalUnits);
        result.TotalCostYen.Should().Be(detail.TotalCostYen);
        result.TotalBenefitYen.Should().Be(detail.BenefitYen);
        result.TotalBurdenYen.Should().Be(detail.BurdenYen);
    }

    [Fact]
    public void Caps_burden_at_certificate_monthly_cap()
    {
        // 700*20*10.00=140,000円。1割相当額=14,000円だがcap=4,600円 → 負担は4,600円に制限される。
        var result = ClaimCalculator.Calculate(SyntheticMasters(), new ClaimCalculationRequest(
            Month, DefaultContext(), "region-a", "b-type",
            [new RecipientClaimSource(RecipientA, BilledDays: 20, BenefitRatePercent: 90, CertificateMonthlyCapYen: 4600, BurdenCategory: SyntheticBurdenCategory)]));

        var detail = result.Details.Should().ContainSingle().Subject;
        detail.TotalCostYen.Should().Be(140000);
        detail.BurdenYen.Should().Be(4600);
        detail.BenefitYen.Should().Be(140000 - 4600);
    }

    [Fact]
    public void Applies_statutory_burden_when_certificate_cap_is_not_binding()
    {
        // capが十分大きく1割相当額を制限しない場合、1割相当額をそのまま利用者負担とする。
        // UnboundedSyntheticCapYenはテスト専用の合成上限であり、証上限「指定なし」をnullで表すことはしない
        // （ADR 0025のfail-closed方針。CertificateMonthlyCapYenは必須int、確認済みの証拠から入る）。
        var result = ClaimCalculator.Calculate(SyntheticMasters(), new ClaimCalculationRequest(
            Month, DefaultContext(), "region-a", "b-type",
            [new RecipientClaimSource(RecipientA, BilledDays: 20, BenefitRatePercent: 90, CertificateMonthlyCapYen: UnboundedSyntheticCapYen, BurdenCategory: SyntheticBurdenCategory)]));

        var detail = result.Details.Should().ContainSingle().Subject;
        detail.TotalCostYen.Should().Be(140000);
        detail.BurdenYen.Should().Be(14000);
        detail.BenefitYen.Should().Be(126000);
    }

    [Fact]
    public void Aggregates_totals_as_sum_of_details_across_multiple_recipients()
    {
        // 2名（BilledDaysが異なる）を投入し、Total*は必ずDetails.Sum(...)と一致するというΣ不変条件を検証する。
        var result = ClaimCalculator.Calculate(SyntheticMasters(), new ClaimCalculationRequest(
            Month, DefaultContext(), "region-a", "b-type",
            [
                new RecipientClaimSource(RecipientA, BilledDays: 20, BenefitRatePercent: 90, CertificateMonthlyCapYen: UnboundedSyntheticCapYen, BurdenCategory: SyntheticBurdenCategory),
                new RecipientClaimSource(RecipientB, BilledDays: 15, BenefitRatePercent: 90, CertificateMonthlyCapYen: 3000, BurdenCategory: SyntheticBurdenCategory),
            ]));

        result.Details.Should().HaveCount(2);
        result.TotalUnits.Should().Be(result.Details.Sum(d => d.TotalUnits));
        result.TotalCostYen.Should().Be(result.Details.Sum(d => d.TotalCostYen));
        result.TotalBenefitYen.Should().Be(result.Details.Sum(d => d.BenefitYen));
        result.TotalBurdenYen.Should().Be(result.Details.Sum(d => d.BurdenYen));
    }

    [Fact]
    public void Throws_when_region_unit_price_is_missing()
        => FluentActions.Invoking(() => ClaimCalculator.Calculate(SyntheticMasters(), new ClaimCalculationRequest(
                Month, DefaultContext(), "region-unknown", "b-type",
                [new RecipientClaimSource(RecipientA, BilledDays: 20, BenefitRatePercent: 90, CertificateMonthlyCapYen: UnboundedSyntheticCapYen, BurdenCategory: SyntheticBurdenCategory)])))
            .Should().Throw<ClaimCalculationException>()
            .Which.Code.Should().Be(ClaimCalculationErrorCode.RegionUnitPriceUnavailable);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(32)]
    public void Rejects_invalid_billed_days(int days)
        => FluentActions.Invoking(() => ClaimCalculator.Calculate(SyntheticMasters(), new ClaimCalculationRequest(
                Month, DefaultContext(), "region-a", "b-type",
                [new RecipientClaimSource(RecipientA, BilledDays: days, BenefitRatePercent: 90, CertificateMonthlyCapYen: UnboundedSyntheticCapYen, BurdenCategory: SyntheticBurdenCategory)])))
            .Should().Throw<ClaimCalculationException>()
            .Which.Code.Should().Be(ClaimCalculationErrorCode.InvalidInput);

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Rejects_invalid_benefit_rate_percent(int benefitRatePercent)
        => FluentActions.Invoking(() => ClaimCalculator.Calculate(SyntheticMasters(), new ClaimCalculationRequest(
                Month, DefaultContext(), "region-a", "b-type",
                [new RecipientClaimSource(RecipientA, BilledDays: 20, BenefitRatePercent: benefitRatePercent, CertificateMonthlyCapYen: UnboundedSyntheticCapYen, BurdenCategory: SyntheticBurdenCategory)])))
            .Should().Throw<ClaimCalculationException>()
            .Which.Code.Should().Be(ClaimCalculationErrorCode.InvalidInput);

    [Fact]
    public void Accepts_benefit_rate_percent_zero_as_full_statutory_burden_before_cap()
    {
        // BenefitRatePercent=0（給付率0%）→ 負担割合100/100 → capが十分大きければ1割相当額算定前の
        // 統計的負担＝総費用額そのものが利用者負担となる。benefit = cost − burdenの関係も同時に検証する。
        var result = ClaimCalculator.Calculate(SyntheticMasters(), new ClaimCalculationRequest(
            Month, DefaultContext(), "region-a", "b-type",
            [new RecipientClaimSource(RecipientA, BilledDays: 20, BenefitRatePercent: 0, CertificateMonthlyCapYen: UnboundedSyntheticCapYen, BurdenCategory: SyntheticBurdenCategory)]));

        var detail = result.Details.Should().ContainSingle().Subject;
        detail.TotalCostYen.Should().Be(140000);
        detail.BurdenYen.Should().Be(140000);
        detail.BenefitYen.Should().Be(0);
        detail.BenefitYen.Should().Be(detail.TotalCostYen - detail.BurdenYen);
    }

    [Fact]
    public void Accepts_benefit_rate_percent_hundred_as_zero_burden()
    {
        // BenefitRatePercent=100（給付率100%）→ 負担割合0/100 → 利用者負担は0円。
        var result = ClaimCalculator.Calculate(SyntheticMasters(), new ClaimCalculationRequest(
            Month, DefaultContext(), "region-a", "b-type",
            [new RecipientClaimSource(RecipientA, BilledDays: 20, BenefitRatePercent: 100, CertificateMonthlyCapYen: UnboundedSyntheticCapYen, BurdenCategory: SyntheticBurdenCategory)]));

        var detail = result.Details.Should().ContainSingle().Subject;
        detail.TotalCostYen.Should().Be(140000);
        detail.BurdenYen.Should().Be(0);
        detail.BenefitYen.Should().Be(140000);
        detail.BenefitYen.Should().Be(detail.TotalCostYen - detail.BurdenYen);
    }

    [Fact]
    public void Rejects_negative_certificate_monthly_cap()
        => FluentActions.Invoking(() => ClaimCalculator.Calculate(SyntheticMasters(), new ClaimCalculationRequest(
                Month, DefaultContext(), "region-a", "b-type",
                [new RecipientClaimSource(RecipientA, BilledDays: 20, BenefitRatePercent: 90, CertificateMonthlyCapYen: -1, BurdenCategory: SyntheticBurdenCategory)])))
            .Should().Throw<ClaimCalculationException>()
            .Which.Code.Should().Be(ClaimCalculationErrorCode.InvalidInput);

    [Fact]
    public void Rounding_rules_apply_throws_for_unknown_rule_id()
        => FluentActions.Invoking(() => ClaimRoundingRules.Apply("claim.rounding.unknown.v1", 1.5m))
            .Should().Throw<ClaimCalculationException>()
            .Which.Code.Should().Be(ClaimCalculationErrorCode.RoundingRuleUnavailable);

    [Fact]
    public void Rounding_rules_floor_cost_and_burden_to_yen()
    {
        ClaimRoundingRules.Apply(ClaimRoundingRules.CostFloorYenV1, 152892.74m).Should().Be(152892);
        ClaimRoundingRules.Apply(ClaimRoundingRules.BurdenFloorYenV1, 15289.2m).Should().Be(15289);
    }

    [Fact]
    public void Rounding_rules_units_half_up_rounds_away_from_zero_at_the_half()
    {
        // UnitsHalfUpV1はMidpointRounding.AwayFromZero（四捨五入）であり、既定のbanker's roundingに
        // 依存しない。X.5の丸め方向を直接検証する（10.5→11、-10.5→-11）。
        ClaimRoundingRules.Apply(ClaimRoundingRules.UnitsHalfUpV1, 10.5m).Should().Be(11);
        ClaimRoundingRules.Apply(ClaimRoundingRules.UnitsHalfUpV1, -10.5m).Should().Be(-11);
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
        DefaultConditionSelectors,
        new BaseComponentPassThroughRule("base-a", "step-base", null, BillingUnit.PerDay),
        [new ClaimComponentRef(ClaimComponentMasterKind.BasicRewards, "base-a", ClaimComponentRole.Base)],
        new ServiceMonth(2024, 4),
        null,
        [SourceRef()]);

    private static RegionUnitPriceMasterRow RegionUnitPrice() => new(
        "price-a", "region-a", "b-type", 10.00m, new ServiceMonth(2024, 4), null, [SourceRef()]);

    private static BurdenCapMasterRow BurdenCap() => new(
        "burden-cap-a", SyntheticBurdenCategory, SyntheticBurdenCategoryCapYen,
        new ServiceMonth(2024, 4), null, [SourceRef()]);

    private static ClaimCalculationMasterBundle SyntheticMasters() => new(
        BasicRewards: [BasicReward()],
        UnitAdjustments: [],
        RegionUnitPrices: [RegionUnitPrice()],
        BurdenCaps: [BurdenCap()],
        TransitionRules: [],
        ServiceCodes: [ServiceCode()],
        ConditionDefinitions: DefaultConditions());
}
