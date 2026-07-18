using FluentAssertions;
using Tsumugi.Application.Claim;
using Tsumugi.Domain.Logic.Claim.Models;
using Xunit;
using Kit = Tsumugi.Application.Tests.UseCases.Claim.ClaimPreparationTestKit;

namespace Tsumugi.Application.Tests.Claim;

public sealed class ClaimCalculationRequestBuilderTests
{
    [Fact]
    public void Build_produces_request_with_statutory_benefit_rate_and_confirmed_cap()
    {
        var result = ClaimCalculationRequestBuilder.Build(Kit.Snapshot(), Kit.Month, Kit.Tokens());

        result.Issues.Should().BeEmpty();
        result.Request.Should().NotBeNull();
        var request = result.Request!;
        request.Month.Should().Be(Kit.Month);
        request.RegionKey.Should().Be("region-a");
        request.ServiceKind.Should().Be("b-type");
        request.Conditions.RewardSystem.Should().Be("b-type");
        request.Conditions.CapacityHeadcount.Should().Be(20);
        request.Conditions.StaffingKey.Should().Be("staff-a");
        request.Conditions.AverageWageBandOption.Should().Be(
            new AverageWageBandOption(AverageWageBandOptionKind.Numeric, 5));
        request.Conditions.R8ReformStatus.Should().Be(R8ReformStatus.NotApplicableBeforeR8);
        // 再エンコード後、PaymentBand tokenは基本報酬解決に使わない（average-wage-band整数条件が担う）。
        request.Conditions.PaymentBand.Should().BeEmpty();

        var source = request.Recipients.Should().ContainSingle().Subject;
        source.RecipientId.Should().Be(Kit.RecipientId);
        source.BilledDays.Should().Be(2);
        source.BenefitRatePercent.Should().Be(90);
        source.CertificateMonthlyCapYen.Should().Be(9300);
    }

    [Fact]
    public void Build_excludes_recipients_without_billed_days_from_the_claim()
    {
        var snapshot = Kit.Snapshot(
            recipientIds: [Kit.RecipientId, Kit.SecondRecipientId],
            inputs: [Kit.Input(), Kit.Input(Kit.SecondRecipientId)],
            evidences: [Kit.Evidence(), Kit.Evidence()],
            billedDays: new Dictionary<Guid, int>
            {
                [Kit.RecipientId] = 2,
                [Kit.SecondRecipientId] = 0,
            },
            certificateCounts: new Dictionary<Guid, int>
            {
                [Kit.RecipientId] = 1,
                [Kit.SecondRecipientId] = 1,
            });

        var result = ClaimCalculationRequestBuilder.Build(snapshot, Kit.Month, Kit.Tokens());

        result.Issues.Should().BeEmpty();
        result.Request!.Recipients.Should().ContainSingle(source => source.RecipientId == Kit.RecipientId);
    }

    [Fact]
    public void Build_reports_structurally_missing_capacity_and_staffing_inputs()
    {
        var tokens = Kit.Tokens(capacityHeadcount: null, staffingKey: null);

        var result = ClaimCalculationRequestBuilder.Build(Kit.Snapshot(), Kit.Month, tokens);

        result.Request.Should().BeNull();
        result.Issues.Should().Contain(issue =>
            issue.Code == ClaimPreparationIssueCode.MissingRequiredField
            && issue.FieldCode == "OfficeClaimProfile.CapacityHeadcount"
            && issue.Destination == ClaimInputDestination.ClaimInput);
        result.Issues.Should().Contain(issue =>
            issue.Code == ClaimPreparationIssueCode.MissingRequiredField
            && issue.FieldCode == "OfficeClaimProfile.StaffingClass"
            && issue.Destination == ClaimInputDestination.ClaimInput);
    }

    [Fact]
    public void Build_reports_missing_region_and_reward_system_tokens()
    {
        var result = ClaimCalculationRequestBuilder.Build(
            Kit.Snapshot(), Kit.Month, tokens: null);

        result.Request.Should().BeNull();
        result.Issues.Should().Contain(issue => issue.FieldCode == "Office.RegionGrade");
        result.Issues.Should().Contain(issue => issue.FieldCode == "Office.ServiceCategory");
    }

    [Fact]
    public void Build_rejects_non_numeric_band_option_without_guessing()
    {
        // ADR 0023: FiledTransition（公式option 8）はservice code resolverへ渡さない。
        var profile = Kit.Profile(
            bandOption: new AverageWageBandOption(AverageWageBandOptionKind.FiledTransition, 8));

        var result = ClaimCalculationRequestBuilder.Build(
            Kit.Snapshot(profile: profile), Kit.Month, Kit.Tokens());

        result.Request.Should().BeNull();
        result.Issues.Should().Contain(issue =>
            issue.Code == ClaimPreparationIssueCode.MissingRequiredField
            && issue.FieldCode == "OfficeClaimProfile.AverageWageBandOption");
    }

    [Fact]
    public void Build_requires_reform_status()
    {
        var profile = Kit.Profile(reformStatus: null);

        var result = ClaimCalculationRequestBuilder.Build(
            Kit.Snapshot(profile: profile), Kit.Month, Kit.Tokens());

        result.Request.Should().BeNull();
        result.Issues.Should().Contain(issue =>
            issue.FieldCode == "OfficeClaimProfile.ReformStatus");
    }

    [Fact]
    public void Build_requires_confirmed_entered_cap_before_constructing_sources()
    {
        var result = ClaimCalculationRequestBuilder.Build(
            Kit.Snapshot(evidences: [Kit.Evidence(capYen: null)]), Kit.Month, Kit.Tokens());

        result.Request.Should().BeNull();
        result.Issues.Should().Contain(issue =>
            issue.Code == ClaimPreparationIssueCode.MissingRequiredEvidence
            && issue.RecipientId == Kit.RecipientId
            && issue.FieldCode == "CertificateClaimEvidence.Effective");
    }

    [Fact]
    public void Build_requires_original_confirmation_before_constructing_sources()
    {
        var result = ClaimCalculationRequestBuilder.Build(
            Kit.Snapshot(evidences: [Kit.Evidence(originalDocumentReference: null)]),
            Kit.Month,
            Kit.Tokens());

        result.Request.Should().BeNull();
        result.Issues.Should().Contain(issue =>
            issue.Code == ClaimPreparationIssueCode.OriginalEvidenceUnconfirmed
            && issue.RecipientId == Kit.RecipientId
            && issue.FieldCode == "CertificateClaimEvidence.Original");
    }

    [Fact]
    public void Build_without_profile_reports_missing_profile_evidence()
    {
        var result = ClaimCalculationRequestBuilder.Build(
            Kit.Snapshot(includeProfile: false), Kit.Month, Kit.Tokens());

        result.Request.Should().BeNull();
        result.Issues.Should().Contain(issue =>
            issue.Code == ClaimPreparationIssueCode.MissingRequiredEvidence
            && issue.FieldCode == "OfficeClaimProfile.Effective");
    }
}
