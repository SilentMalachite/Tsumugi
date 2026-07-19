using FluentAssertions;
using Tsumugi.Application.Claim;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;
using Kit = Tsumugi.Application.Tests.UseCases.Claim.ClaimPreparationTestKit;

namespace Tsumugi.Application.Tests.Claim;

/// <summary>
/// Task 13 (ADR 0023): 経過措置guard（対象月transition-rules行 × profile宣言値）の単体検証。
/// 値はすべて合成語彙（production seedの正準トークンへ依存しない）。
/// </summary>
public sealed class OfficeClaimProfileTransitionGuardTests
{
    private static readonly AverageWageBandOption Numeric5 =
        new(AverageWageBandOptionKind.Numeric, 5);

    private static ClaimSourceRef SourceRef() => new(
        "doc-1",
        new string('0', 64),
        "loc",
        ClaimSourceEvidenceRole.Authoritative,
        [ClaimSourceSupport.MasterValues, ClaimSourceSupport.EffectivePeriod]);

    private static OfficeClaimProfileTransitionRuleMasterRow Rule(
        string key = "transition-a",
        string masterVersion = "master-v1",
        IReadOnlyDictionary<R8ReformStatus, IReadOnlyCollection<AverageWageBandOption>>?
            optionsByStatus = null) =>
        new(
            key,
            new ClaimMasterVersion(masterVersion),
            [Numeric5],
            optionsByStatus ?? new Dictionary<R8ReformStatus, IReadOnlyCollection<AverageWageBandOption>>
            {
                [R8ReformStatus.NotApplicableBeforeR8] = [Numeric5],
            },
            new DateOnly(2026, 6, 1),
            FiledTransitionExclusiveEndRule.AddYearsExclusive,
            1,
            new ServiceMonth(2024, 4),
            null,
            [SourceRef()]);

    [Fact]
    public void Passes_when_the_month_rule_allows_the_declared_status_and_option()
    {
        OfficeClaimProfileTransitionGuard.Validate([Rule()], Kit.Profile())
            .Should().BeEmpty();
    }

    [Fact]
    public void Skips_when_the_profile_is_missing_because_readiness_owns_that_issue()
    {
        OfficeClaimProfileTransitionGuard.Validate([Rule()], profile: null)
            .Should().BeEmpty();
    }

    [Fact]
    public void Skips_non_numeric_options_because_the_request_builder_owns_that_issue()
    {
        var profile = Kit.Profile(
            bandOption: new AverageWageBandOption(AverageWageBandOptionKind.FiledTransition, 8));

        OfficeClaimProfileTransitionGuard.Validate([Rule()], profile)
            .Should().BeEmpty();
    }

    [Fact]
    public void Fails_closed_when_the_month_has_no_rule_for_the_profile_master_version()
    {
        var issues = OfficeClaimProfileTransitionGuard.Validate(
            [Rule(masterVersion: "master-v2")], Kit.Profile());

        issues.Should().ContainSingle(issue =>
            issue.Code == ClaimPreparationIssueCode.ReformTransitionMismatch
            && issue.FieldCode == "OfficeClaimProfile.MasterVersion"
            && issue.RecipientId == null);
    }

    [Fact]
    public void Fails_closed_when_the_month_has_ambiguous_rules_for_the_profile_master_version()
    {
        var issues = OfficeClaimProfileTransitionGuard.Validate(
            [Rule(key: "transition-a"), Rule(key: "transition-b")], Kit.Profile());

        issues.Should().ContainSingle(issue =>
            issue.Code == ClaimPreparationIssueCode.ReformTransitionMismatch
            && issue.FieldCode == "OfficeClaimProfile.MasterVersion");
    }

    [Fact]
    public void Fails_closed_when_the_declared_status_has_no_allowed_option_set()
    {
        var issues = OfficeClaimProfileTransitionGuard.Validate(
            [Rule()], Kit.Profile(reformStatus: R8ReformStatus.ReformExempt));

        issues.Should().ContainSingle(issue =>
            issue.Code == ClaimPreparationIssueCode.ReformTransitionMismatch
            && issue.FieldCode == "OfficeClaimProfile.AverageWageBandOption");
    }

    [Fact]
    public void Fails_closed_when_the_declared_option_is_outside_the_status_set()
    {
        var rule = Rule(optionsByStatus: new Dictionary<R8ReformStatus, IReadOnlyCollection<AverageWageBandOption>>
        {
            [R8ReformStatus.NotApplicableBeforeR8] =
                [new AverageWageBandOption(AverageWageBandOptionKind.Numeric, 1)],
        });

        var issues = OfficeClaimProfileTransitionGuard.Validate([rule], Kit.Profile());

        issues.Should().ContainSingle(issue =>
            issue.Code == ClaimPreparationIssueCode.ReformTransitionMismatch
            && issue.FieldCode == "OfficeClaimProfile.AverageWageBandOption");
    }
}
