using FluentAssertions;
using Tsumugi.Application.Claim;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Logic.Claim.Models;
using Xunit;
using Kit = Tsumugi.Application.Tests.UseCases.Claim.ClaimPreparationTestKit;

namespace Tsumugi.Application.Tests.Claim;

public sealed class ClaimPreparationContextBuilderTests
{
    [Fact]
    public void Build_maps_office_values_recipient_values_and_valid_evidence_states()
    {
        var input = Kit.Input() with { UpperLimitManagedAmountYen = 1234, StandardUsageDayTotal = 20 };
        var snapshot = Kit.Snapshot(inputs: [input]);

        var result = ClaimPreparationContextBuilder.Build(
            snapshot, Kit.Office(), masterVersionAvailable: true);

        result.Issues.Should().BeEmpty();
        var context = result.Context;
        context.OfficeValues["Office.PostalCode"].StringValue.Should().Be("100-0001");
        context.OfficeValues["Office.Address"].Kind.Should().Be(ClaimPreparationValueKind.Text);
        context.OfficeValues["Office.PhoneNumber"].StringValue.Should().Be("03-0000-0000");
        context.OfficeValues["Office.RepresentativeTitleAndName"].StringValue.Should().Be("施設長 テスト");

        var recipient = context.Recipients.Should().ContainSingle().Subject;
        recipient.RecipientId.Should().Be(Kit.RecipientId);
        recipient.EffectiveCertificateCount.Should().Be(1);
        recipient.CertificateClaimEvidence.Should().Be(ClaimPreparationEvidenceState.Valid);
        recipient.UpperLimitManagementStatement.Should().Be(ClaimPreparationEvidenceState.NotApplicable);
        recipient.Values["ClaimInput.UpperLimitManagedAmountYen"].NumberValue.Should().Be(1234);
        recipient.Values["ClaimInput.StandardUsageDayTotal"].NumberValue.Should().Be(20);
        recipient.Values["ClaimInput.UpperLimitManagementResult"].Kind
            .Should().Be(ClaimPreparationValueKind.NotApplicable);

        context.CalculationEvidence.MasterVersion.Should().Be(ClaimPreparationEvidenceState.Valid);
        context.CalculationEvidence.AverageWageAnnualEvidence.Should().Be(ClaimPreparationEvidenceState.Valid);
        context.CalculationEvidence.OfficeClaimProfile.Should().Be(ClaimPreparationEvidenceState.Valid);
    }

    [Fact]
    public void Build_reports_missing_claim_input_and_marks_values_not_applicable()
    {
        var snapshot = Kit.Snapshot(inputs: []);

        var result = ClaimPreparationContextBuilder.Build(
            snapshot, Kit.Office(), masterVersionAvailable: true);

        result.Issues.Should().ContainSingle(issue =>
            issue.Code == ClaimPreparationIssueCode.MissingRequiredField
            && issue.RecipientId == Kit.RecipientId
            && issue.FieldCode == "ClaimInput.Effective"
            && issue.Destination == ClaimInputDestination.ClaimInput);
        result.Context.Recipients[0].Values["ClaimInput.UpperLimitManagedAmountYen"].Kind
            .Should().Be(ClaimPreparationValueKind.NotApplicable);
    }

    [Fact]
    public void Build_reports_missing_office_and_missing_master_version()
    {
        var snapshot = Kit.Snapshot(includeProfile: false);

        var result = ClaimPreparationContextBuilder.Build(
            snapshot, office: null, masterVersionAvailable: false);

        result.Issues.Should().ContainSingle(issue =>
            issue.Code == ClaimPreparationIssueCode.MissingRequiredField
            && issue.RecipientId == null
            && issue.FieldCode == "Office.Effective"
            && issue.Destination == ClaimInputDestination.Office);
        result.Context.OfficeValues.Should().BeEmpty();
        result.Context.CalculationEvidence.MasterVersion.Should().Be(ClaimPreparationEvidenceState.Missing);
        result.Context.CalculationEvidence.OfficeClaimProfile.Should().Be(ClaimPreparationEvidenceState.Missing);
    }

    [Fact]
    public void Build_surfaces_certificate_counts_without_choosing_a_representative()
    {
        var snapshot = Kit.Snapshot(
            evidenceByRecipient: new Dictionary<Guid, CertificateClaimEvidence>(),
            certificateCounts: new Dictionary<Guid, int> { [Kit.RecipientId] = 2 });

        var result = ClaimPreparationContextBuilder.Build(
            snapshot, Kit.Office(), masterVersionAvailable: true);

        var recipient = result.Context.Recipients.Should().ContainSingle().Subject;
        recipient.EffectiveCertificateCount.Should().Be(2);
        recipient.CertificateClaimEvidence.Should().Be(ClaimPreparationEvidenceState.Unknown);
    }

    [Fact]
    public void Build_resolves_certificate_evidence_independently_per_recipient()
    {
        // Task 9b: evidenceは利用者IDを鍵とする明示的な辞書対応のため、片方だけ根拠が
        // 登録されていてももう片方へ波及しない（旧: 件数不一致で全員Missingにするposition
        // fail-closedは、明示キーにより不要になった）。
        var snapshot = Kit.Snapshot(
            recipientIds: [Kit.RecipientId, Kit.SecondRecipientId],
            inputs: [Kit.Input(), Kit.Input(Kit.SecondRecipientId)],
            evidenceByRecipient: new Dictionary<Guid, CertificateClaimEvidence>
            {
                [Kit.RecipientId] = Kit.Evidence(),
            },
            billedDays: new Dictionary<Guid, int>
            {
                [Kit.RecipientId] = 2,
                [Kit.SecondRecipientId] = 3,
            },
            certificateCounts: new Dictionary<Guid, int>
            {
                [Kit.RecipientId] = 1,
                [Kit.SecondRecipientId] = 1,
            });

        var result = ClaimPreparationContextBuilder.Build(
            snapshot, Kit.Office(), masterVersionAvailable: true);

        result.Context.Recipients.Should().Contain(recipient =>
            recipient.RecipientId == Kit.RecipientId
            && recipient.CertificateClaimEvidence == ClaimPreparationEvidenceState.Valid);
        result.Context.Recipients.Should().Contain(recipient =>
            recipient.RecipientId == Kit.SecondRecipientId
            && recipient.CertificateClaimEvidence == ClaimPreparationEvidenceState.Missing);
    }

    [Fact]
    public void Build_marks_unconfirmed_original_evidence()
    {
        var snapshot = Kit.Snapshot(
            evidenceByRecipient: new Dictionary<Guid, CertificateClaimEvidence>
            {
                [Kit.RecipientId] = Kit.Evidence(originalDocumentReference: null),
            });

        var result = ClaimPreparationContextBuilder.Build(
            snapshot, Kit.Office(), masterVersionAvailable: true);

        result.Context.Recipients[0].CertificateClaimEvidence
            .Should().Be(ClaimPreparationEvidenceState.OriginalUnconfirmed);
    }

    [Fact]
    public void Build_marks_unentered_monthly_cost_cap_as_missing_evidence()
    {
        var snapshot = Kit.Snapshot(evidenceByRecipient: new Dictionary<Guid, CertificateClaimEvidence>
        {
            [Kit.RecipientId] = Kit.Evidence(capYen: null),
        });

        var result = ClaimPreparationContextBuilder.Build(
            snapshot, Kit.Office(), masterVersionAvailable: true);

        result.Context.Recipients[0].CertificateClaimEvidence
            .Should().Be(ClaimPreparationEvidenceState.Missing);
    }

    [Fact]
    public void Build_requires_statement_when_upper_limit_management_applies()
    {
        var evidence = Kit.Evidence() with
        {
            UpperLimitManagementApplicability = UpperLimitManagementApplicability.Applicable,
            UpperLimitManagementOfficeNumber = "1310000002",
        };
        var snapshot = Kit.Snapshot(evidenceByRecipient: new Dictionary<Guid, CertificateClaimEvidence>
        {
            [Kit.RecipientId] = evidence,
        });

        var result = ClaimPreparationContextBuilder.Build(
            snapshot, Kit.Office(), masterVersionAvailable: true);

        result.Context.Recipients[0].UpperLimitManagementStatement
            .Should().Be(ClaimPreparationEvidenceState.Missing);
    }
}
