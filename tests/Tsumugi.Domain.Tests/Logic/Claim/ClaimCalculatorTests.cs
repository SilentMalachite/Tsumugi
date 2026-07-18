using FluentAssertions;
using Tsumugi.Domain.Logic.Claim;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Tests.Logic.Claim;

public sealed class ClaimCalculatorTests
{
    private static readonly ServiceMonth Month = new(2025, 4);
    private static readonly Guid RecipientA = Guid.Parse("11111111-1111-1111-1111-111111111111");

    // 合成マスタは定員を頭数(int)で表す閾値条件（capacity, less-than-or-equal, 20）で表現する。
    // ServiceCodeResolverTestsと同じヘルパ形式（baseUnits=700, unitPriceYen=10.00m）を使う。
    private static readonly string[] DefaultConditionSelectors =
        ["cond-system-b", "cond-band-a", "cond-cap-a", "cond-staff-a"];

    [Fact]
    public void Calculates_basic_reward_only_recipient()
    {
        var result = ClaimCalculator.Calculate(SyntheticMasters(), new ClaimCalculationRequest(
            Month, DefaultContext(), "region-a", "b-type",
            [new RecipientClaimSource(RecipientA, BilledDays: 20, BenefitRatePercent: 90, CertificateMonthlyCapYen: 0)]));

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
            [new RecipientClaimSource(RecipientA, BilledDays: 20, BenefitRatePercent: 90, CertificateMonthlyCapYen: 4600)]));

        var detail = result.Details.Should().ContainSingle().Subject;
        detail.TotalCostYen.Should().Be(140000);
        detail.BurdenYen.Should().Be(4600);
        detail.BenefitYen.Should().Be(140000 - 4600);
    }

    [Fact]
    public void Applies_statutory_burden_when_certificate_cap_is_not_specified()
    {
        // cap未指定（null）は「証上限の指定なし」を表し、1割相当額をそのまま利用者負担とする。
        var result = ClaimCalculator.Calculate(SyntheticMasters(), new ClaimCalculationRequest(
            Month, DefaultContext(), "region-a", "b-type",
            [new RecipientClaimSource(RecipientA, BilledDays: 20, BenefitRatePercent: 90, CertificateMonthlyCapYen: null)]));

        var detail = result.Details.Should().ContainSingle().Subject;
        detail.TotalCostYen.Should().Be(140000);
        detail.BurdenYen.Should().Be(14000);
        detail.BenefitYen.Should().Be(126000);
    }

    [Fact]
    public void Throws_when_region_unit_price_is_missing()
        => FluentActions.Invoking(() => ClaimCalculator.Calculate(SyntheticMasters(), new ClaimCalculationRequest(
                Month, DefaultContext(), "region-unknown", "b-type",
                [new RecipientClaimSource(RecipientA, BilledDays: 20, BenefitRatePercent: 90, CertificateMonthlyCapYen: null)])))
            .Should().Throw<ClaimCalculationException>()
            .Which.Code.Should().Be(ClaimCalculationErrorCode.RegionUnitPriceUnavailable);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(32)]
    public void Rejects_invalid_billed_days(int days)
        => FluentActions.Invoking(() => ClaimCalculator.Calculate(SyntheticMasters(), new ClaimCalculationRequest(
                Month, DefaultContext(), "region-a", "b-type",
                [new RecipientClaimSource(RecipientA, BilledDays: days, BenefitRatePercent: 90, CertificateMonthlyCapYen: null)])))
            .Should().Throw<ClaimCalculationException>()
            .Which.Code.Should().Be(ClaimCalculationErrorCode.InvalidInput);

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Rejects_invalid_benefit_rate_percent(int benefitRatePercent)
        => FluentActions.Invoking(() => ClaimCalculator.Calculate(SyntheticMasters(), new ClaimCalculationRequest(
                Month, DefaultContext(), "region-a", "b-type",
                [new RecipientClaimSource(RecipientA, BilledDays: 20, BenefitRatePercent: benefitRatePercent, CertificateMonthlyCapYen: null)])))
            .Should().Throw<ClaimCalculationException>()
            .Which.Code.Should().Be(ClaimCalculationErrorCode.InvalidInput);

    [Fact]
    public void Rejects_negative_certificate_monthly_cap()
        => FluentActions.Invoking(() => ClaimCalculator.Calculate(SyntheticMasters(), new ClaimCalculationRequest(
                Month, DefaultContext(), "region-a", "b-type",
                [new RecipientClaimSource(RecipientA, BilledDays: 20, BenefitRatePercent: 90, CertificateMonthlyCapYen: -1)])))
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

    private static ClaimCalculationMasterBundle SyntheticMasters() => new(
        BasicRewards: [BasicReward()],
        UnitAdjustments: [],
        RegionUnitPrices: [RegionUnitPrice()],
        BurdenCaps: [],
        TransitionRules: [],
        ServiceCodes: [ServiceCode()],
        ConditionDefinitions: DefaultConditions());
}
