using FluentAssertions;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;
using Tsumugi.Infrastructure.ClaimMasters;

namespace Tsumugi.Infrastructure.Tests.ClaimMasters;

/// <summary>
/// R8-06境界（2026-05→2026-06）の切替をproduction seedで固定する（Task 13・ADR 0023）。
/// 経過措置ruleはtransition-rules.jsonのseedから供給され、体制届の公式option番号を
/// C#へ直書きしない（値の出典はADR 0023の版付き対応表とsource inventory manifest）。
/// </summary>
public sealed class ClaimMasterR8BoundaryTests
{
    private static readonly ServiceMonth May2026 = new(2026, 5);
    private static readonly ServiceMonth June2026 = new(2026, 6);

    private static readonly JsonClaimMasterProvider Provider =
        JsonClaimMasterProvider.LoadEmbedded();

    private static AverageWageBandOption Numeric(int code)
        => new(AverageWageBandOptionKind.Numeric, code);

    private static AverageWageBandOption FiledTransition()
        => new(AverageWageBandOptionKind.FiledTransition, 8);

    private static AverageWageBandOption ProductionActivitySupport()
        => new(AverageWageBandOptionKind.ProductionActivitySupport, 10);

    private static OfficeClaimProfileTransitionRuleMasterRow SingleTransitionRule(
        ServiceMonth month)
    {
        var rules = Provider.ResolveCalculationMasters(month).TransitionRules;
        rules.Should().ContainSingle(
            $"月{month.Year}-{month.Month:00}の経過措置ruleは一意でなければならない");
        return rules[0];
    }

    [Theory]
    [InlineData(2024, 4, "claim-master-r6-04")]
    [InlineData(2024, 5, "claim-master-r6-04")]
    [InlineData(2024, 6, "claim-master-r6-06")]
    [InlineData(2025, 9, "claim-master-r6-06")]
    [InlineData(2026, 5, "claim-master-r6-06")]
    [InlineData(2026, 6, "claim-master-r8-06")]
    public void Transition_rules_resolve_a_single_band_edition_per_month(
        int year, int month, string expectedMasterVersion)
    {
        var rule = SingleTransitionRule(new ServiceMonth(year, month));

        rule.MasterVersion.Value.Should().Be(expectedMasterVersion);
        rule.R8EffectiveDate.Should().Be(new DateOnly(2026, 6, 1));
        rule.FiledTransitionEndRule.Should().Be(
            FiledTransitionExclusiveEndRule.AddYearsExclusive);
        rule.FiledTransitionDurationYears.Should().Be(1);
    }

    [Fact]
    public void R6_band_edition_serves_official_options_1_to_10_until_may_2026()
    {
        var rule = SingleTransitionRule(May2026);

        rule.AllowedAverageWageBandOptions.Should().BeEquivalentTo(
        [
            Numeric(1), Numeric(2), Numeric(3), Numeric(4), Numeric(5),
            Numeric(6), Numeric(7), FiledTransition(), Numeric(9),
            ProductionActivitySupport(),
        ]);
        rule.AllowedOptionsByR8ReformStatus.Keys.Should().BeEquivalentTo(
            [R8ReformStatus.NotApplicableBeforeR8],
            "R8施行前の版に改定対象・対象外の区分群は存在しない（ADR 0023）");
        rule.AllowedOptionsByR8ReformStatus[R8ReformStatus.NotApplicableBeforeR8]
            .Should().BeEquivalentTo(rule.AllowedAverageWageBandOptions);
    }

    [Fact]
    public void R8_band_edition_partitions_official_options_by_reform_status_from_june_2026()
    {
        var rule = SingleTransitionRule(June2026);

        rule.AllowedAverageWageBandOptions.Should().HaveCount(22);
        rule.AllowedOptionsByR8ReformStatus.Keys.Should().BeEquivalentTo(
        [
            R8ReformStatus.ReformTarget,
            R8ReformStatus.ReformExempt,
            R8ReformStatus.UnchangedBelow15000,
        ], "施行前状態はR8-06版で使用できず、option 10のR8状態対応は一次資料未確定（open-questions）");

        rule.AllowedOptionsByR8ReformStatus[R8ReformStatus.ReformTarget]
            .Should().BeEquivalentTo(
            [
                FiledTransition(),
                Numeric(11), Numeric(12), Numeric(13), Numeric(14), Numeric(15),
                Numeric(16), Numeric(17), Numeric(18), Numeric(19), Numeric(20),
                Numeric(21), Numeric(22),
            ], "改定対象は新12区分option（11〜22）と新規指定FiledTransitionだけを使える");
        rule.AllowedOptionsByR8ReformStatus[R8ReformStatus.ReformExempt]
            .Should().BeEquivalentTo(
                [Numeric(1), Numeric(2), Numeric(3), Numeric(4), Numeric(5), Numeric(6)],
                "改定対象外は従前6区分（option 1〜6）を継続する");
        rule.AllowedOptionsByR8ReformStatus[R8ReformStatus.UnchangedBelow15000]
            .Should().BeEquivalentTo(
                [Numeric(7), Numeric(9)],
                "1万5千円未満（option 7・9）は区分境界が変わらない");
    }

    [Fact]
    public void Reform_target_offices_cannot_keep_r6_numeric_band_options_from_june_2026()
    {
        var rule = SingleTransitionRule(June2026);
        var reformTarget = rule.AllowedOptionsByR8ReformStatus[R8ReformStatus.ReformTarget];

        foreach (var r6NumericCode in new[] { 1, 2, 3, 4, 5, 6, 7, 9 })
            reformTarget.Should().NotContain(
                Numeric(r6NumericCode),
                "改定対象がR6数値区分のまま2026-06以降を請求することはフェイルクローズする");
    }

    [Theory]
    [InlineData("claim-master-r6-04")]
    [InlineData("claim-master-r6-06")]
    [InlineData("claim-master-r8-06")]
    public void Profile_policies_resolve_for_each_seeded_band_edition(string masterVersion)
    {
        var action = () => Provider.Resolve(new ClaimMasterVersion(masterVersion));

        action.Should().NotThrow();
    }
}
