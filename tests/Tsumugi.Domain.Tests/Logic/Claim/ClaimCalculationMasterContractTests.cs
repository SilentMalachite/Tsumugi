using FluentAssertions;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Tests.Logic.Claim;

public sealed class ClaimCalculationMasterContractTests
{
    [Fact]
    public void Claim_source_ref_retains_provenance_and_supported_fields()
    {
        IReadOnlyList<ClaimSourceSupport> supports =
        [
            ClaimSourceSupport.ServiceIdentity,
            ClaimSourceSupport.EffectivePeriod,
        ];

        var source = new ClaimSourceRef(
            "document-1",
            "sha256-1",
            "workbook-order=38;row=7",
            ClaimSourceEvidenceRole.Authoritative,
            supports);

        source.DocumentId.Should().Be("document-1");
        source.Sha256.Should().Be("sha256-1");
        source.Locator.Should().Be("workbook-order=38;row=7");
        source.EvidenceRole.Should().Be(ClaimSourceEvidenceRole.Authoritative);
        source.Supports.Should().BeSameAs(supports);
    }

    [Theory]
    [InlineData(837)]
    [InlineData(-5)]
    public void Fixed_composite_unit_rule_retains_nonzero_signed_final_units(int finalUnits)
    {
        var rule = new FixedCompositeUnitRule(finalUnits, BillingUnit.PerDay);

        rule.FinalUnits.Should().Be(finalUnits);
        rule.BillingUnit.Should().Be(BillingUnit.PerDay);
    }

    [Fact]
    public void Fixed_composite_unit_rule_rejects_zero_final_units()
    {
        var action = () => new FixedCompositeUnitRule(0, BillingUnit.PerDay);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Unit_adjustment_amount_union_retains_kind_specific_values()
    {
        UnitAdjustmentAmount fixedUnits = new FixedUnitsAmount(12);
        UnitAdjustmentAmount perCount =
            new UnitsPerCountAmount(93, "previous-year-six-month-employment-count");
        UnitAdjustmentAmount percentage = new PercentageOfTargetAmount(
            0.10m,
            PercentageBaseScope.MonthlyTargetUnitSum,
            "selector:monthly-target",
            2);
        UnitAdjustmentAmount prorated = new ProratedUnitsAmount(
            500,
            "medical-coordination-v-visiting-nurse-count",
            "medical-coordination-v-supported-recipient-count",
            8);

        fixedUnits.Should().Be(new FixedUnitsAmount(12));
        perCount.Should().Be(
            new UnitsPerCountAmount(93, "previous-year-six-month-employment-count"));
        percentage.Should().Be(
            new PercentageOfTargetAmount(
                0.10m,
                PercentageBaseScope.MonthlyTargetUnitSum,
                "selector:monthly-target",
                2));
        prorated.Should().Be(
            new ProratedUnitsAmount(
                500,
                "medical-coordination-v-visiting-nurse-count",
                "medical-coordination-v-supported-recipient-count",
                8));
    }

    [Fact]
    public void Unit_addition_rule_retains_amount_step_rounding_and_billing_unit()
    {
        var amount = new ProratedUnitsAmount(
            500,
            "medical-coordination-v-visiting-nurse-count",
            "medical-coordination-v-supported-recipient-count",
            8);

        var rule = new UnitAdditionRule(
            "medical-coordination-v",
            amount,
            "claim.step.units.service-code.prorate-by-recipient-count.v1",
            "claim.rounding.units.half-up.v1",
            BillingUnit.PerDay);

        rule.AdjustmentComponentKey.Should().Be("medical-coordination-v");
        rule.Amount.Should().BeSameAs(amount);
        rule.CalculationStepId.Should()
            .Be("claim.step.units.service-code.prorate-by-recipient-count.v1");
        rule.RoundingRuleId.Should().Be("claim.rounding.units.half-up.v1");
        rule.BillingUnit.Should().Be(BillingUnit.PerDay);
    }

    [Fact]
    public void Formula_modes_retain_pass_through_and_factor_chain_boundaries()
    {
        var passThrough = new BaseComponentPassThroughRule(
            "basic-1",
            "claim.step.units.service-code.base-component-pass-through.v1",
            null,
            BillingUnit.PerDay);
        IReadOnlyList<ServiceCodeFormulaFactor> factors =
        [
            new(
                1,
                0.7m,
                ["plan-not-created", "first-two-months"],
                "claim.step.units.per-service-code.percentage.v1",
                "claim.rounding.units.half-up.v1"),
        ];
        var factorChain = new FactorChainRule("basic-1", factors, BillingUnit.PerDay);

        passThrough.BaseComponentKey.Should().Be("basic-1");
        passThrough.CalculationStepId.Should()
            .Be("claim.step.units.service-code.base-component-pass-through.v1");
        passThrough.RoundingRuleId.Should().BeNull();
        factorChain.BaseComponentKey.Should().Be("basic-1");
        factorChain.Factors.Should().BeSameAs(factors);
        factorChain.Factors[0].Rate.Should().Be(0.7m);
        factorChain.Factors[0].ConditionSelectors.Should()
            .Equal("plan-not-created", "first-two-months");
    }

    [Fact]
    public void Condition_and_component_refs_retain_closed_values()
    {
        IReadOnlyList<ClaimSourceRef> sources = [Source()];
        ClaimConditionOperand operand = new ClaimConditionIntegerOperand(20);
        var condition = new ClaimConditionDefinition(
            "capacity-up-to-20",
            new ServiceMonth(2024, 4),
            null,
            ClaimConditionKind.Capacity,
            ClaimConditionOperator.LessThanOrEqual,
            operand,
            sources);
        var component = new ClaimComponentRef(
            ClaimComponentMasterKind.BasicRewards,
            "basic-1",
            ClaimComponentRole.Base);

        condition.Operand.Should().BeSameAs(operand);
        condition.SourceRefs.Should().BeSameAs(sources);
        component.MasterKind.Should().Be(ClaimComponentMasterKind.BasicRewards);
        component.Role.Should().Be(ClaimComponentRole.Base);
    }

    [Fact]
    public void Master_rows_keep_base_final_and_adjustment_units_distinct()
    {
        var effectiveFrom = new ServiceMonth(2024, 4);
        IReadOnlyList<ClaimSourceRef> sources = [Source()];
        var basic = new BasicRewardMasterRow(
            "basic-1",
            "band-1",
            "staffing-1",
            "capacity-1",
            "462980",
            837,
            effectiveFrom,
            null,
            sources);
        var adjustmentAmount = new UnitsPerCountAmount(
            93,
            "previous-year-six-month-employment-count");
        var adjustment = new UnitAdjustmentMasterRow(
            "employment-transition-1",
            adjustmentAmount,
            "claim.step.units.service-code.multiply-count.v1",
            null,
            BillingUnit.PerDay,
            effectiveFrom,
            null,
            sources);
        IReadOnlyList<string> selectors = ["selector:basic"];
        IReadOnlyList<string> conditions = ["capacity-up-to-20"];
        IReadOnlyList<ClaimComponentRef> components =
        [
            new(ClaimComponentMasterKind.BasicRewards, "basic-1", ClaimComponentRole.Base),
        ];
        ServiceCodeUnitRule unitRule = new FixedCompositeUnitRule(837, BillingUnit.PerDay);
        var serviceCode = new ServiceCodeMasterRow(
            "service-462980",
            "462980",
            "就継ＢⅠ１１",
            "employment-continuation-support-b",
            selectors,
            conditions,
            unitRule,
            components,
            effectiveFrom,
            null,
            sources);

        basic.BaseUnits.Should().Be(837);
        adjustment.Amount.Should().BeSameAs(adjustmentAmount);
        serviceCode.OfficialLabel.Should().Be("就継ＢⅠ１１");
        serviceCode.UnitRule.Should().BeSameAs(unitRule);
        serviceCode.ComponentRefs.Should().BeSameAs(components);
    }

    [Fact]
    public void Claim_calculation_master_bundle_retains_all_v2_collections()
    {
        IReadOnlyList<BasicRewardMasterRow> basicRewards = [];
        IReadOnlyList<UnitAdjustmentMasterRow> unitAdjustments = [];
        IReadOnlyList<RegionUnitPriceMasterRow> regionUnitPrices = [];
        IReadOnlyList<BurdenCapMasterRow> burdenCaps = [];
        IReadOnlyList<OfficeClaimProfileTransitionRuleMasterRow> transitionRules = [];
        IReadOnlyList<ServiceCodeMasterRow> serviceCodes = [];
        IReadOnlyList<ClaimConditionDefinition> conditionDefinitions = [];

        var bundle = new ClaimCalculationMasterBundle(
            basicRewards,
            unitAdjustments,
            regionUnitPrices,
            burdenCaps,
            transitionRules,
            serviceCodes,
            conditionDefinitions);

        bundle.BasicRewards.Should().BeSameAs(basicRewards);
        bundle.UnitAdjustments.Should().BeSameAs(unitAdjustments);
        bundle.RegionUnitPrices.Should().BeSameAs(regionUnitPrices);
        bundle.BurdenCaps.Should().BeSameAs(burdenCaps);
        bundle.TransitionRules.Should().BeSameAs(transitionRules);
        bundle.ServiceCodes.Should().BeSameAs(serviceCodes);
        bundle.ConditionDefinitions.Should().BeSameAs(conditionDefinitions);
    }

    private static ClaimSourceRef Source() =>
        new(
            "document-1",
            "sha256-1",
            "workbook-order=38;row=7",
            ClaimSourceEvidenceRole.Authoritative,
            [ClaimSourceSupport.MasterValues, ClaimSourceSupport.EffectivePeriod]);
}
