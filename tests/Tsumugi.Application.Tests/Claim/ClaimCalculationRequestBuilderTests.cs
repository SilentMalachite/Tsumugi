using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Claim;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
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
        // Task 12（ADR 0022）: 既定証（PaymentBurden=General2）→区分key"general-2"。
        source.BurdenCategory.Should().Be("general-2");
        source.UpperLimitResult.Should().BeNull();
        source.UpperLimitManagedAmountYen.Should().BeNull();
    }

    [Fact]
    public void Build_threads_addition_counts_with_certificate_gated_meal_days()
    {
        // Task 11（ADR 0028決定5）: 欠席時対応・送迎はDailyRecord縮約値をそのまま、
        // 食事提供日数は受給者証のMealProvisionApplicableで対象判定してから渡す。
        var snapshot = Kit.Snapshot(
            certificateByRecipient: new Dictionary<Guid, Certificate>
            {
                [Kit.RecipientId] = Kit.Certificate(mealProvisionApplicable: true),
            },
            additionDailyCountsByRecipient: new Dictionary<Guid, ClaimAdditionDailyCounts>
            {
                [Kit.RecipientId] = new(AbsenceSupportDays: 2, MealProvidedDays: 1, TransportOneWayCount: 4),
            });

        var result = ClaimCalculationRequestBuilder.Build(snapshot, Kit.Month, Kit.Tokens());

        result.Issues.Should().BeEmpty();
        var source = result.Request!.Recipients.Should().ContainSingle().Subject;
        source.AbsenceSupportCount.Should().Be(2);
        source.MealProvidedDays.Should().Be(1);
        source.TransportOneWayCount.Should().Be(4);
        // ストレージgap（ADR 0028決定5）の実績は常に0（推測しない）。
        source.InitialPeriodServiceDays.Should().Be(0);
        source.TransportSamePremisesOneWayCount.Should().Be(0);
    }

    [Fact]
    public void Build_zeroes_meal_days_when_certificate_says_not_applicable()
    {
        var snapshot = Kit.Snapshot(
            certificateByRecipient: new Dictionary<Guid, Certificate>
            {
                [Kit.RecipientId] = Kit.Certificate(mealProvisionApplicable: false),
            },
            additionDailyCountsByRecipient: new Dictionary<Guid, ClaimAdditionDailyCounts>
            {
                [Kit.RecipientId] = new(AbsenceSupportDays: 0, MealProvidedDays: 2, TransportOneWayCount: 0),
            });

        var result = ClaimCalculationRequestBuilder.Build(snapshot, Kit.Month, Kit.Tokens());

        result.Issues.Should().BeEmpty();
        result.Request!.Recipients.Should().ContainSingle()
            .Which.MealProvidedDays.Should().Be(0);
    }

    [Fact]
    public void Build_fails_closed_when_the_certificate_entity_is_missing()
    {
        // Task 12（ADR 0022）: 負担区分の解決にCertificateが必須（食事提供実績の有無を問わない）。
        // 証実体がsnapshotにない場合は推測せずissue化する。
        var snapshot = Kit.Snapshot(
            certificateByRecipient: new Dictionary<Guid, Certificate>(),
            additionDailyCountsByRecipient: new Dictionary<Guid, ClaimAdditionDailyCounts>
            {
                [Kit.RecipientId] = new(AbsenceSupportDays: 0, MealProvidedDays: 1, TransportOneWayCount: 0),
            });

        var result = ClaimCalculationRequestBuilder.Build(snapshot, Kit.Month, Kit.Tokens());

        result.Request.Should().BeNull();
        result.Issues.Should().Contain(issue =>
            issue.Code == ClaimPreparationIssueCode.MissingRequiredEvidence
            && issue.RecipientId == Kit.RecipientId
            && issue.FieldCode == "Certificate.PaymentBurden");
    }

    [Fact]
    public void Build_fails_closed_when_the_certificate_burden_category_is_unspecified()
    {
        // Task 12（ADR 0022）: Unspecifiedは制度額0の区分ではなく非入力状態のため算定不能。
        var snapshot = Kit.Snapshot(
            certificateByRecipient: new Dictionary<Guid, Certificate>
            {
                [Kit.RecipientId] = Kit.Certificate(paymentBurden: PaymentBurdenCategory.Unspecified),
            });

        var result = ClaimCalculationRequestBuilder.Build(snapshot, Kit.Month, Kit.Tokens());

        result.Request.Should().BeNull();
        result.Issues.Should().Contain(issue =>
            issue.Code == ClaimPreparationIssueCode.MissingRequiredField
            && issue.RecipientId == Kit.RecipientId
            && issue.FieldCode == "Certificate.PaymentBurden");
    }

    [Fact]
    public void Build_resolves_effective_capability_flag_keys_into_the_context()
    {
        var snapshot = Kit.Snapshot(officeCapabilities:
        [
            Kit.Capability(new Dictionary<string, bool>
            {
                ["cap.synthetic.a"] = true,
                ["cap.synthetic.b"] = false,
            }),
        ]);

        var result = ClaimCalculationRequestBuilder.Build(snapshot, Kit.Month, Kit.Tokens());

        result.Issues.Should().BeEmpty();
        result.Request!.Conditions.OfficeCapabilityKeys.Should().BeEquivalentTo(["cap.synthetic.a"]);
    }

    [Fact]
    public void Build_treats_missing_capability_records_as_an_empty_key_set()
    {
        var result = ClaimCalculationRequestBuilder.Build(
            Kit.Snapshot(officeCapabilities: []), Kit.Month, Kit.Tokens());

        result.Issues.Should().BeEmpty();
        result.Request!.Conditions.OfficeCapabilityKeys.Should().NotBeNull();
        result.Request.Conditions.OfficeCapabilityKeys.Should().BeEmpty();
    }

    [Fact]
    public void Build_fails_closed_when_effective_capability_selection_is_ambiguous()
    {
        // ADR 0021: Period.StartとCreatedAtがともに同値の複数候補は曖昧（算定不能）。
        var snapshot = Kit.Snapshot(officeCapabilities:
        [
            Kit.Capability(),
            Kit.Capability(new Dictionary<string, bool> { ["cap.synthetic.b"] = true }),
        ]);

        var result = ClaimCalculationRequestBuilder.Build(snapshot, Kit.Month, Kit.Tokens());

        result.Request.Should().BeNull();
        result.Issues.Should().Contain(issue =>
            issue.Code == ClaimPreparationIssueCode.InvalidEffectiveHistory
            && issue.RecipientId == null
            && issue.FieldCode == "OfficeCapability.Flags");
    }

    [Fact]
    public void Build_passes_count_selector_bindings_through_to_the_request()
    {
        var bindings = new Dictionary<string, ClaimCountMetric>(StringComparer.Ordinal)
        {
            ["count-a"] = ClaimCountMetric.ServiceDays,
        };

        var result = ClaimCalculationRequestBuilder.Build(
            Kit.Snapshot(), Kit.Month, Kit.Tokens(countSelectorBindings: bindings));

        result.Issues.Should().BeEmpty();
        result.Request!.CountSelectorBindings.Should().BeSameAs(bindings);
    }

    [Fact]
    public void Build_excludes_recipients_without_billed_days_from_the_claim()
    {
        var snapshot = Kit.Snapshot(
            recipientIds: [Kit.RecipientId, Kit.SecondRecipientId],
            inputs: [Kit.Input(), Kit.Input(Kit.SecondRecipientId)],
            evidenceByRecipient: new Dictionary<Guid, CertificateClaimEvidence>
            {
                [Kit.RecipientId] = Kit.Evidence(),
                [Kit.SecondRecipientId] = Kit.Evidence(),
            },
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
    public void Build_reports_a_dedicated_conflict_issue_when_region_key_sources_disagree()
    {
        // controller decision 2026-07-19 (Task 9b fix round): 両ソース不一致はフェイルクローズ専用issue。
        // 汎用の「地域区分未解決」(Office.RegionGrade)issueとは重複させない。
        var tokens = Kit.Tokens(regionKey: null, regionKeyConflict: true);

        var result = ClaimCalculationRequestBuilder.Build(Kit.Snapshot(), Kit.Month, tokens);

        result.Request.Should().BeNull();
        result.Issues.Should().ContainSingle(issue =>
            issue.Code == ClaimPreparationIssueCode.RegionKeySourceConflict
            && issue.RecipientId == null
            && issue.FieldCode == "OfficeClaimProfile.RegionKey"
            && issue.Destination == ClaimInputDestination.ClaimInput);
        result.Issues.Should().NotContain(issue => issue.FieldCode == "Office.RegionGrade");
    }

    [Fact]
    public void Build_does_not_report_a_conflict_issue_when_region_key_sources_agree_or_profile_overrides_cleanly()
    {
        var result = ClaimCalculationRequestBuilder.Build(
            Kit.Snapshot(), Kit.Month, Kit.Tokens(regionKey: "region-a", regionKeyConflict: false));

        result.Issues.Should().NotContain(issue => issue.Code == ClaimPreparationIssueCode.RegionKeySourceConflict);
        result.Request.Should().NotBeNull();
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
            Kit.Snapshot(evidenceByRecipient: new Dictionary<Guid, CertificateClaimEvidence>
            {
                [Kit.RecipientId] = Kit.Evidence(capYen: null),
            }),
            Kit.Month,
            Kit.Tokens());

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
            Kit.Snapshot(evidenceByRecipient: new Dictionary<Guid, CertificateClaimEvidence>
            {
                [Kit.RecipientId] = Kit.Evidence(originalDocumentReference: null),
            }),
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
