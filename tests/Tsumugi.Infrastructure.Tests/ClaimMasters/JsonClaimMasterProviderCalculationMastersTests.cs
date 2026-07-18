using System.Text.Json.Nodes;
using FluentAssertions;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;
using Tsumugi.Infrastructure.ClaimMasters;

namespace Tsumugi.Infrastructure.Tests.ClaimMasters;

public sealed class JsonClaimMasterProviderCalculationMastersTests
{
    [Fact]
    public void ResolveCalculationMasters_filters_rows_by_effective_month()
    {
        var provider = JsonClaimMasterProvider.LoadEmbedded();
        var masters = provider.ResolveCalculationMasters(new ServiceMonth(2025, 4));
        masters.BasicRewards.Should().OnlyContain(row =>
            row.EffectiveFrom <= new ServiceMonth(2025, 4)
            && (row.EffectiveTo == null || new ServiceMonth(2025, 4) <= row.EffectiveTo));
    }

    [Fact]
    public void ResolveCalculationMasters_throws_for_month_before_any_release()
    {
        var provider = JsonClaimMasterProvider.LoadEmbedded();
        FluentActions.Invoking(() => provider.ResolveCalculationMasters(new ServiceMonth(2000, 1)))
            .Should().Throw<Tsumugi.Application.Abstractions.ClaimMasterPolicyUnavailableException>();
    }

    // Every embedded seed row has effectiveTo: null, so the two tests above never exercise the
    // EffectiveTo-inclusive half of FilterByMonth's boundary (`month <= end`), and only assert on
    // BasicRewards out of the seven filtered collections. This test builds a synthetic v2 master
    // bundle on the internal seam (reusing ClaimMasterSchemaPhase31Tests' ValidMasters()/CreateProvider
    // harness) so a closed period ("2024-04".."2026-05") and its successor ("2026-06"..null) exist in
    // four different collections, and checks the inclusive end-of-period boundary month explicitly.
    [Fact]
    public void ResolveCalculationMasters_includes_closed_period_row_through_inclusive_effectiveTo_boundary_across_collections()
    {
        var masters = ClaimMasterSchemaPhase31Tests.ValidMasters();
        SplitEntryIntoClosedPeriodAndSuccessor(masters, "basic-rewards.json", "basic-1");
        SplitEntryIntoClosedPeriodAndSuccessor(masters, "region-unit-prices.json", "region-1");
        SplitEntryIntoClosedPeriodAndSuccessor(masters, "service-codes.json", "service-fixed");
        SplitConditionIntoClosedPeriodAndSuccessor(masters, "capacity-up-to-20");

        var provider = ClaimMasterSchemaPhase31Tests.CreateProvider(masters);

        AssertOnlyActiveRevisionPresent(
            provider.ResolveCalculationMasters(new ServiceMonth(2025, 4)),
            expectedEffectiveFrom: "2024-04");
        AssertOnlyActiveRevisionPresent(
            provider.ResolveCalculationMasters(new ServiceMonth(2026, 5)),
            expectedEffectiveFrom: "2024-04");
        AssertOnlyActiveRevisionPresent(
            provider.ResolveCalculationMasters(new ServiceMonth(2026, 6)),
            expectedEffectiveFrom: "2026-06");
    }

    private static void AssertOnlyActiveRevisionPresent(
        ClaimCalculationMasterBundle bundle,
        string expectedEffectiveFrom)
    {
        var basicRewards = bundle.BasicRewards.Where(row => row.Key == "basic-1").ToArray();
        basicRewards.Should().ContainSingle();
        basicRewards[0].EffectiveFrom.ToString().Should().Be(expectedEffectiveFrom);

        var regionUnitPrices = bundle.RegionUnitPrices
            .Where(row => row.Key == "region-1").ToArray();
        regionUnitPrices.Should().ContainSingle();
        regionUnitPrices[0].EffectiveFrom.ToString().Should().Be(expectedEffectiveFrom);

        var serviceCodes = bundle.ServiceCodes
            .Where(row => row.Key == "service-fixed").ToArray();
        serviceCodes.Should().ContainSingle();
        serviceCodes[0].EffectiveFrom.ToString().Should().Be(expectedEffectiveFrom);

        var conditionDefinitions = bundle.ConditionDefinitions
            .Where(row => row.Key == "capacity-up-to-20").ToArray();
        conditionDefinitions.Should().ContainSingle();
        conditionDefinitions[0].EffectiveFrom.ToString().Should().Be(expectedEffectiveFrom);
    }

    private static void SplitEntryIntoClosedPeriodAndSuccessor(
        Dictionary<string, string> masters,
        string fileName,
        string key)
    {
        var root = JsonNode.Parse(masters[fileName])!.AsObject();
        var entries = root["entries"]!.AsArray();
        var entry = entries
            .Select(node => node!.AsObject())
            .Single(candidate => candidate["key"]!.GetValue<string>() == key);
        entry["effectiveTo"] = "2026-05";
        var successor = entry.DeepClone().AsObject();
        successor["effectiveFrom"] = "2026-06";
        successor["effectiveTo"] = null;
        entries.Add(successor);
        masters[fileName] = root.ToJsonString();
    }

    private static void SplitConditionIntoClosedPeriodAndSuccessor(
        Dictionary<string, string> masters,
        string key)
    {
        var root = JsonNode.Parse(masters["service-codes.json"])!.AsObject();
        var conditions = root["conditionDefinitions"]!.AsArray();
        var condition = conditions
            .Select(node => node!.AsObject())
            .Single(candidate => candidate["key"]!.GetValue<string>() == key);
        condition["effectiveTo"] = "2026-05";
        var successor = condition.DeepClone().AsObject();
        successor["effectiveFrom"] = "2026-06";
        successor["effectiveTo"] = null;
        conditions.Add(successor);
        masters["service-codes.json"] = root.ToJsonString();
    }
}
