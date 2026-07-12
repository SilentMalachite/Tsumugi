using FluentAssertions;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Tests.Logic.Claim;

public sealed class ClaimCalculationMasterContractTests
{
    [Fact]
    public void Claim_source_locator_retains_all_values()
    {
        var source = new ClaimSourceLocator("document-1", "sha256-1", "sheet:basic!A2:F2");

        source.DocumentId.Should().Be("document-1");
        source.Sha256.Should().Be("sha256-1");
        source.Locator.Should().Be("sheet:basic!A2:F2");
    }

    [Fact]
    public void Basic_reward_master_row_retains_all_values()
    {
        var effectiveFrom = new ServiceMonth(2026, 6);
        var effectiveTo = new ServiceMonth(2027, 5);
        var source = Source();

        var row = new BasicRewardMasterRow(
            "basic-1",
            "payment-band-1",
            "staffing-1",
            "capacity-1",
            "461001",
            100,
            effectiveFrom,
            effectiveTo,
            source);

        row.Key.Should().Be("basic-1");
        row.PaymentBand.Should().Be("payment-band-1");
        row.StaffingKey.Should().Be("staffing-1");
        row.CapacityKey.Should().Be("capacity-1");
        row.ServiceCode.Should().Be("461001");
        row.Units.Should().Be(100);
        row.EffectiveFrom.Should().Be(effectiveFrom);
        row.EffectiveTo.Should().Be(effectiveTo);
        row.Source.Should().BeSameAs(source);
    }

    [Fact]
    public void Percentage_adjustment_master_row_retains_all_values()
    {
        var effectiveFrom = new ServiceMonth(2026, 6);
        var source = Source();

        var row = new PercentageAdjustmentMasterRow(
            "addition-1",
            0.15m,
            PercentageBaseScope.MonthlyTargetUnitSum,
            PercentageApplicationKind.Subtract,
            "selector:basic",
            2,
            "rounding-rule-1",
            "calculation-step-1",
            effectiveFrom,
            null,
            source);

        row.Key.Should().Be("addition-1");
        row.Percentage.Should().Be(0.15m);
        row.BaseScope.Should().Be(PercentageBaseScope.MonthlyTargetUnitSum);
        row.ApplicationKind.Should().Be(PercentageApplicationKind.Subtract);
        row.TargetSelector.Should().Be("selector:basic");
        row.CalculationOrder.Should().Be(2);
        row.RoundingRuleId.Should().Be("rounding-rule-1");
        row.CalculationStepId.Should().Be("calculation-step-1");
        row.EffectiveFrom.Should().Be(effectiveFrom);
        row.EffectiveTo.Should().BeNull();
        row.Source.Should().BeSameAs(source);
    }

    [Fact]
    public void Region_unit_price_master_row_retains_all_values()
    {
        var effectiveFrom = new ServiceMonth(2026, 6);
        var effectiveTo = new ServiceMonth(2027, 5);
        var source = Source();

        var row = new RegionUnitPriceMasterRow(
            "region-price-1",
            "region-1",
            "employment-continuation-support-b",
            11.20m,
            effectiveFrom,
            effectiveTo,
            source);

        row.Key.Should().Be("region-price-1");
        row.RegionKey.Should().Be("region-1");
        row.ServiceKind.Should().Be("employment-continuation-support-b");
        row.UnitPriceYen.Should().Be(11.20m);
        row.EffectiveFrom.Should().Be(effectiveFrom);
        row.EffectiveTo.Should().Be(effectiveTo);
        row.Source.Should().BeSameAs(source);
    }

    [Fact]
    public void Burden_cap_master_row_retains_all_values()
    {
        var effectiveFrom = new ServiceMonth(2026, 6);
        var effectiveTo = new ServiceMonth(2027, 5);
        var source = Source();

        var row = new BurdenCapMasterRow(
            "burden-cap-1",
            "category-1",
            37_200,
            effectiveFrom,
            effectiveTo,
            source);

        row.Key.Should().Be("burden-cap-1");
        row.BurdenCategory.Should().Be("category-1");
        row.CapYen.Should().Be(37_200);
        row.EffectiveFrom.Should().Be(effectiveFrom);
        row.EffectiveTo.Should().Be(effectiveTo);
        row.Source.Should().BeSameAs(source);
    }

    [Fact]
    public void Office_claim_profile_transition_rule_master_row_retains_all_values()
    {
        var masterVersion = new ClaimMasterVersion("claim-master-r8-06");
        var option = new AverageWageBandOption(AverageWageBandOptionKind.Numeric, 1);
        IReadOnlyList<AverageWageBandOption> allowedOptions = new[] { option };
        IReadOnlyDictionary<R8ReformStatus, IReadOnlyCollection<AverageWageBandOption>> allowedByStatus =
            new Dictionary<R8ReformStatus, IReadOnlyCollection<AverageWageBandOption>>
            {
                [R8ReformStatus.ReformTarget] = new[] { option },
            };
        var effectiveFrom = new ServiceMonth(2026, 6);
        var source = Source();

        var row = new OfficeClaimProfileTransitionRuleMasterRow(
            "office-profile-policy",
            masterVersion,
            allowedOptions,
            allowedByStatus,
            new DateOnly(2026, 6, 1),
            FiledTransitionExclusiveEndRule.AddYearsExclusive,
            1,
            effectiveFrom,
            null,
            source);

        row.Key.Should().Be("office-profile-policy");
        row.MasterVersion.Should().Be(masterVersion);
        row.AllowedAverageWageBandOptions.Should().BeSameAs(allowedOptions);
        row.AllowedOptionsByR8ReformStatus.Should().BeSameAs(allowedByStatus);
        row.R8EffectiveDate.Should().Be(new DateOnly(2026, 6, 1));
        row.FiledTransitionEndRule.Should().Be(FiledTransitionExclusiveEndRule.AddYearsExclusive);
        row.FiledTransitionDurationYears.Should().Be(1);
        row.EffectiveFrom.Should().Be(effectiveFrom);
        row.EffectiveTo.Should().BeNull();
        row.Source.Should().BeSameAs(source);
    }

    [Fact]
    public void Service_code_master_row_retains_all_values()
    {
        IReadOnlyList<string> selectors = new[] { "selector:basic", "selector:addition" };
        var effectiveFrom = new ServiceMonth(2026, 6);
        var effectiveTo = new ServiceMonth(2027, 5);
        var source = Source();

        var row = new ServiceCodeMasterRow(
            "service-code-1",
            "461001",
            "employment-continuation-support-b",
            selectors,
            effectiveFrom,
            effectiveTo,
            source);

        row.Key.Should().Be("service-code-1");
        row.ServiceCode.Should().Be("461001");
        row.ServiceKind.Should().Be("employment-continuation-support-b");
        row.Selectors.Should().BeSameAs(selectors);
        row.EffectiveFrom.Should().Be(effectiveFrom);
        row.EffectiveTo.Should().Be(effectiveTo);
        row.Source.Should().BeSameAs(source);
    }

    [Fact]
    public void Claim_calculation_master_bundle_retains_all_typed_collections()
    {
        IReadOnlyList<BasicRewardMasterRow> basicRewards = Array.Empty<BasicRewardMasterRow>();
        IReadOnlyList<PercentageAdjustmentMasterRow> percentageAdjustments =
            Array.Empty<PercentageAdjustmentMasterRow>();
        IReadOnlyList<RegionUnitPriceMasterRow> regionUnitPrices = Array.Empty<RegionUnitPriceMasterRow>();
        IReadOnlyList<BurdenCapMasterRow> burdenCaps = Array.Empty<BurdenCapMasterRow>();
        IReadOnlyList<OfficeClaimProfileTransitionRuleMasterRow> transitionRules =
            Array.Empty<OfficeClaimProfileTransitionRuleMasterRow>();
        IReadOnlyList<ServiceCodeMasterRow> serviceCodes = Array.Empty<ServiceCodeMasterRow>();

        var bundle = new ClaimCalculationMasterBundle(
            basicRewards,
            percentageAdjustments,
            regionUnitPrices,
            burdenCaps,
            transitionRules,
            serviceCodes);

        bundle.BasicRewards.Should().BeSameAs(basicRewards);
        bundle.PercentageAdjustments.Should().BeSameAs(percentageAdjustments);
        bundle.RegionUnitPrices.Should().BeSameAs(regionUnitPrices);
        bundle.BurdenCaps.Should().BeSameAs(burdenCaps);
        bundle.TransitionRules.Should().BeSameAs(transitionRules);
        bundle.ServiceCodes.Should().BeSameAs(serviceCodes);
    }

    private static ClaimSourceLocator Source() =>
        new("document-1", "sha256-1", "sheet:basic!A2:F2");
}
