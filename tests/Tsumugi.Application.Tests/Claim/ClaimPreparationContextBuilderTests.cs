using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Claim;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;
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
    public void Build_excludes_zero_activity_recipient_from_blocking_but_keeps_it_visible()
    {
        // Task 9b: 実績0日かつ有効ClaimInputなしの利用者は一覧に残るがissueは出さない
        // （over-strict readinessの是正）。
        var snapshot = Kit.Snapshot(
            inputs: [],
            evidenceByRecipient: new Dictionary<Guid, CertificateClaimEvidence>(),
            certificateCounts: new Dictionary<Guid, int> { [Kit.RecipientId] = 0 },
            billedDays: new Dictionary<Guid, int> { [Kit.RecipientId] = 0 });

        var result = ClaimPreparationContextBuilder.Build(
            snapshot, Kit.Office(), masterVersionAvailable: true);

        result.Issues.Should().BeEmpty();
        var recipient = result.Context.Recipients.Should().ContainSingle().Subject;
        recipient.ExcludedFromReadinessBlocking.Should().BeTrue();
        recipient.EffectiveCertificateCount.Should().Be(0);
    }

    [Fact]
    public void Build_does_not_exclude_zero_activity_recipient_with_corrupt_claim_input_history()
    {
        // 有効ClaimInputが0件ではなく複数件（履歴不整合）の場合は、実績0日でも除外しない。
        var input = Kit.Input();
        var duplicateRootInput = Kit.Input() with { Id = Guid.NewGuid(), RootId = Guid.NewGuid() };
        var snapshot = Kit.Snapshot(
            inputs: [input, duplicateRootInput],
            billedDays: new Dictionary<Guid, int> { [Kit.RecipientId] = 0 });

        var result = ClaimPreparationContextBuilder.Build(
            snapshot, Kit.Office(), masterVersionAvailable: true);

        result.Issues.Should().ContainSingle(issue =>
            issue.Code == ClaimPreparationIssueCode.InvalidEffectiveHistory
            && issue.RecipientId == Kit.RecipientId);
        result.Context.Recipients[0].ExcludedFromReadinessBlocking.Should().BeFalse();
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    public void Build_populates_daily_record_row_scope_from_billed_days(int billedDays, bool expectRowScope)
    {
        // Task 4 fix round: rowPresent(service-performance.daily)を参照するreadiness rule
        // （ServiceStartTime/ServiceEndTime/RecipientConfirmation等）がApplyできるかは、
        // ここでRowScopesへ"service-performance.daily"を積むかどうかで決まる。billedDays
        // （当月の実効Present日数）が1件以上ならその日次行が存在するとみなす。
        var snapshot = Kit.Snapshot(billedDays: new Dictionary<Guid, int> { [Kit.RecipientId] = billedDays });

        var result = ClaimPreparationContextBuilder.Build(
            snapshot, Kit.Office(), masterVersionAvailable: true);

        var recipient = result.Context.Recipients.Should().ContainSingle().Subject;
        recipient.RowScopes.Contains("service-performance.daily").Should().Be(expectRowScope);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Build_populates_intensive_support_row_scope_from_daily_record_aggregate(
        bool intensiveSupportApplied, bool expectRowScope)
    {
        // Phase 3-2 Task 8: rowPresent(service-performance.intensive-support)を参照するreadiness rule
        // （IntensiveSupportEpisode.StartDate）がApplyできるかは、ここでRowScopesへ
        // "service-performance.intensive-support"を積むかどうかで決まる。当月の実効Present日次記録を
        // OR縮約したdailyRecordAggregate.IntensiveSupportApplied（ClaimDailyRecordAggregateの
        // doc-comment参照）がtrueならその月は集中的支援が適用されたとみなす。
        var aggregate = new ClaimDailyRecordAggregate(
            ServiceStartTime: null,
            ServiceEndTime: null,
            SpecialVisitSupportMinutesTotal: 0,
            OffsiteSupportApplied: false,
            MedicalCoordinationType: MedicalCoordinationType.Unspecified,
            TrialUseSupportType: TrialUseSupportType.Unspecified,
            RegionalCollaborationApplied: false,
            IntensiveSupportApplied: intensiveSupportApplied,
            EmergencyAdmissionApplied: false);
        var snapshot = Kit.Snapshot(
            dailyRecordAggregateByRecipient:
                new Dictionary<Guid, ClaimDailyRecordAggregate> { [Kit.RecipientId] = aggregate });

        var result = ClaimPreparationContextBuilder.Build(
            snapshot, Kit.Office(), masterVersionAvailable: true);

        var recipient = result.Context.Recipients.Should().ContainSingle().Subject;
        recipient.RowScopes.Contains("service-performance.intensive-support").Should().Be(expectRowScope);
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

    [Fact]
    public void Build_maps_certificate_contracted_provider_daily_record_and_intensive_support_values_when_present()
    {
        // Task 9c: 14 target path（Certificate.* / ContractedProvider.CertificateEntryNumber /
        // DailyRecord.* / IntensiveSupportEpisode.StartDate）が実データからそのまま写像されることを検証する。
        var certificate = Kit.Certificate(
            municipalityNumber: "131000",
            subsidyMunicipalityNumber: "132000",
            upperLimitManagementProviderNumber: "1310000099");
        var contractedProvider = Kit.ContractedProvider(certificateEntryNumber: 7);
        var dailyRecordAggregate = new ClaimDailyRecordAggregate(
            ServiceStartTime: new TimeOnly(9, 0),
            ServiceEndTime: new TimeOnly(15, 0),
            SpecialVisitSupportMinutesTotal: 30,
            OffsiteSupportApplied: true,
            MedicalCoordinationType: MedicalCoordinationType.TypeI,
            TrialUseSupportType: TrialUseSupportType.TypeI,
            RegionalCollaborationApplied: true,
            IntensiveSupportApplied: true,
            EmergencyAdmissionApplied: true,
            RecipientConfirmation: RecipientConfirmationStatus.Confirmed);
        var intensiveSupportStartDate = new DateOnly(2025, 1, 6);

        var snapshot = Kit.Snapshot(
            certificateByRecipient: new Dictionary<Guid, Certificate> { [Kit.RecipientId] = certificate },
            contractedProviderByRecipient:
                new Dictionary<Guid, ContractedProvider> { [Kit.RecipientId] = contractedProvider },
            dailyRecordAggregateByRecipient:
                new Dictionary<Guid, ClaimDailyRecordAggregate> { [Kit.RecipientId] = dailyRecordAggregate },
            intensiveSupportEpisodeStartDateByRecipient:
                new Dictionary<Guid, DateOnly> { [Kit.RecipientId] = intensiveSupportStartDate });

        var result = ClaimPreparationContextBuilder.Build(
            snapshot, Kit.Office(), masterVersionAvailable: true);

        var values = result.Context.Recipients.Should().ContainSingle().Subject.Values;
        values["Certificate.MunicipalityNumber"].StringValue.Should().Be("131000");
        values["Certificate.SubsidyMunicipalityNumber"].StringValue.Should().Be("132000");
        values["Certificate.UpperLimitManagementProviderNumber"].StringValue.Should().Be("1310000099");
        values["ContractedProvider.CertificateEntryNumber"].NumberValue.Should().Be(7);
        values["DailyRecord.ServiceStartTime"].StringValue.Should().Be("09:00");
        values["DailyRecord.ServiceEndTime"].StringValue.Should().Be("15:00");
        values["DailyRecord.SpecialVisitSupportMinutes"].NumberValue.Should().Be(30);
        values["DailyRecord.OffsiteSupportApplied"].BooleanValue.Should().BeTrue();
        values["DailyRecord.MedicalCoordinationType"].StringValue.Should().Be("TypeI");
        values["DailyRecord.TrialUseSupportType"].StringValue.Should().Be("TypeI");
        values["DailyRecord.RegionalCollaborationApplied"].BooleanValue.Should().BeTrue();
        values["DailyRecord.IntensiveSupportApplied"].BooleanValue.Should().BeTrue();
        values["DailyRecord.EmergencyAdmissionApplied"].BooleanValue.Should().BeTrue();
        values["DailyRecord.RecipientConfirmation"].StringValue.Should().Be("Confirmed");
        values["IntensiveSupportEpisode.StartDate"].DateValue.Should().Be(intensiveSupportStartDate);
    }

    [Fact]
    public void Build_marks_certificate_and_daily_record_values_not_applicable_when_snapshot_carries_no_such_data()
    {
        // Task 9c: snapshotがCertificate/ContractedProvider/DailyRecord/IntensiveSupportEpisodeの
        // 追加データを一切運ばない場合（既存テストの既定snapshot）でも、Values辞書は必ずキーを持ち
        // （Unresolvedにしない）、真偽値/数値/区分系は既定値、文字列/日付系はNotApplicableになる。
        var snapshot = Kit.Snapshot();

        var result = ClaimPreparationContextBuilder.Build(
            snapshot, Kit.Office(), masterVersionAvailable: true);

        var values = result.Context.Recipients.Should().ContainSingle().Subject.Values;
        values["Certificate.MunicipalityNumber"].Kind.Should().Be(ClaimPreparationValueKind.NotApplicable);
        values["Certificate.SubsidyMunicipalityNumber"].Kind.Should().Be(ClaimPreparationValueKind.NotApplicable);
        values["Certificate.UpperLimitManagementProviderNumber"].Kind
            .Should().Be(ClaimPreparationValueKind.NotApplicable);
        values["ContractedProvider.CertificateEntryNumber"].Kind
            .Should().Be(ClaimPreparationValueKind.NotApplicable);
        values["DailyRecord.ServiceStartTime"].Kind.Should().Be(ClaimPreparationValueKind.NotApplicable);
        values["DailyRecord.ServiceEndTime"].Kind.Should().Be(ClaimPreparationValueKind.NotApplicable);
        values["DailyRecord.SpecialVisitSupportMinutes"].NumberValue.Should().Be(0);
        values["DailyRecord.OffsiteSupportApplied"].BooleanValue.Should().BeFalse();
        values["DailyRecord.MedicalCoordinationType"].StringValue.Should().Be("Unspecified");
        values["DailyRecord.TrialUseSupportType"].StringValue.Should().Be("Unspecified");
        values["DailyRecord.RegionalCollaborationApplied"].BooleanValue.Should().BeFalse();
        values["DailyRecord.IntensiveSupportApplied"].BooleanValue.Should().BeFalse();
        values["DailyRecord.EmergencyAdmissionApplied"].BooleanValue.Should().BeFalse();
        values["DailyRecord.RecipientConfirmation"].Kind.Should().Be(ClaimPreparationValueKind.NotApplicable);
        values["IntensiveSupportEpisode.StartDate"].Kind.Should().Be(ClaimPreparationValueKind.NotApplicable);
    }
}
