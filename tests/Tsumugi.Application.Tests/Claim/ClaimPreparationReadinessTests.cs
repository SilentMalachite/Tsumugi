using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Claim;
using Xunit;

namespace Tsumugi.Application.Tests.Claim;

public sealed class ClaimPreparationReadinessTests
{
    [Fact]
    public void Evaluate_reports_always_required_missing_field()
    {
        var sut = Sut(Requirement(
            "Office.PostalCode",
            new ClaimRequirementCondition.Always(),
            ClaimInputDestination.Office));

        var result = sut.Evaluate(Context());

        result.IsReady.Should().BeFalse();
        result.Issues.Should().ContainSingle().Which.Should().Be(
            new ClaimPreparationIssue(
                ClaimPreparationIssueCode.MissingRequiredField,
                null,
                "Office.PostalCode",
                ClaimInputDestination.Office));
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void Evaluate_applies_conditional_requirement(
        bool trigger,
        bool targetPresent,
        bool expectedReady)
    {
        var recipientId = Guid.NewGuid();
        var values = new Dictionary<string, ClaimPreparationValue>
        {
            ["DailyRecord.Applied"] = ClaimPreparationValue.Boolean(trigger),
        };
        if (targetPresent)
            values["IntensiveSupportEpisode.StartDate"] =
                ClaimPreparationValue.Date(new DateOnly(2026, 6, 1));
        var sut = Sut(Requirement(
            "IntensiveSupportEpisode.StartDate",
            new ClaimRequirementCondition.ModelTrue("DailyRecord.Applied"),
            ClaimInputDestination.DailyRecord));

        var result = sut.Evaluate(Context(recipients: [Recipient(recipientId, values)]));

        result.IsReady.Should().Be(expectedReady);
        result.Issues.Should().HaveCount(expectedReady ? 0 : 1);
    }

    [Fact]
    public void Evaluate_treats_explicit_false_as_present()
    {
        var recipientId = Guid.NewGuid();
        var sut = Sut(Requirement(
            "DailyRecord.RegionalCollaborationApplied",
            new ClaimRequirementCondition.ModelTrue(
                "DailyRecord.RegionalCollaborationApplied"),
            ClaimInputDestination.DailyRecord));
        var values = new Dictionary<string, ClaimPreparationValue>
        {
            ["DailyRecord.RegionalCollaborationApplied"] =
                ClaimPreparationValue.Boolean(false),
        };

        var result = sut.Evaluate(Context(recipients: [Recipient(recipientId, values)]));

        result.IsReady.Should().BeTrue();
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_treats_formal_zero_as_present()
    {
        var recipientId = Guid.NewGuid();
        var sut = Sut(Requirement(
            "ClaimInput.MunicipalSubsidyAmountYen",
            new ClaimRequirementCondition.ModelNonZero(
                "ClaimInput.MunicipalSubsidyAmountYen"),
            ClaimInputDestination.ClaimPreparation));
        var values = new Dictionary<string, ClaimPreparationValue>
        {
            ["ClaimInput.MunicipalSubsidyAmountYen"] = ClaimPreparationValue.Number(0),
        };

        var result = sut.Evaluate(Context(recipients: [Recipient(recipientId, values)]));

        result.IsReady.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_treats_explicit_not_applicable_as_ready()
    {
        var recipientId = Guid.NewGuid();
        const string target = "Certificate.SubsidyMunicipalityNumber";
        var sut = Sut(Requirement(
            target,
            new ClaimRequirementCondition.ModelPresent(target)));
        var values = new Dictionary<string, ClaimPreparationValue>
        {
            [target] = ClaimPreparationValue.NotApplicable(),
        };

        var result = sut.Evaluate(Context(recipients: [Recipient(recipientId, values)]));

        result.IsReady.Should().BeTrue();
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_does_not_satisfy_always_required_with_not_applicable()
    {
        var recipientId = Guid.NewGuid();
        const string target = "Certificate.MunicipalityNumber";
        var sut = Sut(Requirement(target, new ClaimRequirementCondition.Always()));
        var values = new Dictionary<string, ClaimPreparationValue>
        {
            [target] = ClaimPreparationValue.NotApplicable(),
        };

        var result = sut.Evaluate(Context(recipients: [Recipient(recipientId, values)]));

        result.Issues.Should().ContainSingle(issue =>
            issue.Code == ClaimPreparationIssueCode.MissingRequiredField
            && issue.FieldCode == target);
    }

    [Fact]
    public void Evaluate_rejects_unresolved_self_conditions()
    {
        var recipientId = Guid.NewGuid();
        var sut = Sut(
            Requirement(
                "DailyRecord.RegionalCollaborationApplied",
                new ClaimRequirementCondition.ModelTrue(
                    "DailyRecord.RegionalCollaborationApplied")),
            Requirement(
                "ClaimInput.MunicipalSubsidyAmountYen",
                new ClaimRequirementCondition.ModelNonZero(
                    "ClaimInput.MunicipalSubsidyAmountYen")),
            Requirement(
                "Certificate.SubsidyMunicipalityNumber",
                new ClaimRequirementCondition.ModelPresent(
                    "Certificate.SubsidyMunicipalityNumber")));

        var result = sut.Evaluate(Context(recipients: [Recipient(recipientId)]));

        result.IsReady.Should().BeFalse();
        result.Issues.Should().HaveCount(3).And.OnlyContain(issue =>
            issue.Code == ClaimPreparationIssueCode.UnresolvedRequirementCondition);
    }

    [Fact]
    public void Evaluate_rejects_multiple_effective_certificates()
    {
        var recipientId = Guid.NewGuid();
        var sut = Sut();
        var recipient = Recipient(recipientId, effectiveCertificateCount: 2);

        var result = sut.Evaluate(Context(recipients: [recipient]));

        result.Issues.Should().ContainSingle().Which.Should().Be(
            new ClaimPreparationIssue(
                ClaimPreparationIssueCode.MultipleEffectiveCertificates,
                recipientId,
                "Certificate.Effective",
                ClaimInputDestination.Certificate));
    }

    [Fact]
    public void Evaluate_rejects_corrupt_effective_history()
    {
        var recipientId = Guid.NewGuid();
        var sut = Sut();
        var recipient = Recipient(
            recipientId,
            certificateEvidence: ClaimPreparationEvidenceState.InvalidHistory);

        var result = sut.Evaluate(Context(recipients: [recipient]));

        result.Issues.Should().ContainSingle(issue =>
            issue.Code == ClaimPreparationIssueCode.InvalidEffectiveHistory
            && issue.RecipientId == recipientId
            && issue.FieldCode == "CertificateClaimEvidence.Effective");
    }

    [Fact]
    public void Evaluate_rejects_missing_master_version()
    {
        var result = Sut().Evaluate(Context(evidence: Evidence(
            masterVersion: ClaimPreparationEvidenceState.Missing)));

        result.Issues.Should().ContainSingle().Which.Should().Be(
            new ClaimPreparationIssue(
                ClaimPreparationIssueCode.MasterVersionUnavailable,
                null,
                "ClaimMaster.Version",
                ClaimInputDestination.ClaimPreparation));
    }

    [Fact]
    public void Evaluate_rejects_unconfirmed_original_evidence()
    {
        var recipientId = Guid.NewGuid();
        var recipient = Recipient(
            recipientId,
            certificateEvidence: ClaimPreparationEvidenceState.OriginalUnconfirmed);

        var result = Sut().Evaluate(Context(recipients: [recipient]));

        result.Issues.Should().ContainSingle().Which.Should().Be(
            new ClaimPreparationIssue(
                ClaimPreparationIssueCode.OriginalEvidenceUnconfirmed,
                recipientId,
                "CertificateClaimEvidence.Original",
                ClaimInputDestination.ClaimInput));
    }

    [Theory]
    [InlineData(ClaimPreparationEvidenceState.Missing, ClaimPreparationEvidenceState.Valid,
        "AverageWageAnnualEvidence.Effective")]
    [InlineData(ClaimPreparationEvidenceState.Valid, ClaimPreparationEvidenceState.Missing,
        "OfficeClaimProfile.Effective")]
    public void Evaluate_rejects_missing_global_calculation_evidence(
        ClaimPreparationEvidenceState averageWage,
        ClaimPreparationEvidenceState officeProfile,
        string expectedFieldCode)
    {
        var result = Sut().Evaluate(Context(evidence: Evidence(
            averageWage: averageWage,
            officeProfile: officeProfile)));

        result.Issues.Should().ContainSingle().Which.Should().Be(
            new ClaimPreparationIssue(
                ClaimPreparationIssueCode.MissingRequiredEvidence,
                null,
                expectedFieldCode,
                ClaimInputDestination.ClaimInput));
    }

    [Fact]
    public void Evaluate_rejects_missing_required_upper_limit_statement()
    {
        var recipientId = Guid.NewGuid();
        var recipient = Recipient(
            recipientId,
            upperLimitEvidence: ClaimPreparationEvidenceState.Missing);

        var result = Sut().Evaluate(Context(recipients: [recipient]));

        result.Issues.Should().ContainSingle().Which.Should().Be(
            new ClaimPreparationIssue(
                ClaimPreparationIssueCode.MissingRequiredEvidence,
                recipientId,
                "UpperLimitManagementStatement.Effective",
                ClaimInputDestination.ClaimInput));
    }

    [Fact]
    public void Evaluate_rejects_evidence_source_mismatch()
    {
        var result = Sut().Evaluate(Context(evidence: Evidence(
            officeProfile: ClaimPreparationEvidenceState.SourceMismatch)));

        result.Issues.Should().ContainSingle().Which.Should().Be(
            new ClaimPreparationIssue(
                ClaimPreparationIssueCode.EvidenceSourceMismatch,
                null,
                "OfficeClaimProfile.Effective",
                ClaimInputDestination.ClaimInput));
    }

    [Fact]
    public void Evaluate_supports_every_closed_condition_node()
    {
        var recipientId = Guid.NewGuid();
        var values = new Dictionary<string, ClaimPreparationValue>
        {
            ["Trigger.Present"] = ClaimPreparationValue.Text("value"),
            ["Trigger.Number"] = ClaimPreparationValue.Number(1),
            ["Trigger.Boolean"] = ClaimPreparationValue.Boolean(true),
            ["Trigger.Code"] = ClaimPreparationValue.Code("TypeI"),
        };
        var requirements = new[]
        {
            Requirement("Target.Present", new ClaimRequirementCondition.ModelPresent("Trigger.Present")),
            Requirement("Target.Number", new ClaimRequirementCondition.ModelNonZero("Trigger.Number")),
            Requirement("Target.Boolean", new ClaimRequirementCondition.ModelTrue("Trigger.Boolean")),
            Requirement("Target.Code", new ClaimRequirementCondition.ModelIn("Trigger.Code", ["TypeI"])),
            Requirement("Target.All", new ClaimRequirementCondition.All(
            [
                new ClaimRequirementCondition.RowPresent("daily"),
                new ClaimRequirementCondition.ModelTrue("Trigger.Boolean"),
            ])),
            Requirement("Target.Any", new ClaimRequirementCondition.Any(
            [
                new ClaimRequirementCondition.ModelTrue("Trigger.Missing"),
                new ClaimRequirementCondition.ModelPresent("Trigger.Present"),
            ])),
        };

        var result = Sut(requirements).Evaluate(Context(recipients:
        [
            Recipient(
                recipientId,
                values,
                rowScopes: new HashSet<string>(["daily"], StringComparer.Ordinal)),
        ]));

        result.Issues.Select(issue => issue.FieldCode).Should().BeEquivalentTo(
            "Target.Present", "Target.Number", "Target.Boolean",
            "Target.Code", "Target.All", "Target.Any");
    }

    [Fact]
    public void Evaluate_excludes_zero_activity_recipient_from_certificate_and_requirement_issues()
    {
        // Task 9b: 実績0日かつ有効ClaimInputなしの利用者はreadinessのブロック評価から除外する
        // （一覧には残るがissueは出さない）。context builder側でこの利用者に印を付ける。
        var recipientId = Guid.NewGuid();
        var sut = Sut(Requirement(
            "Office.PostalCode",
            new ClaimRequirementCondition.Always(),
            ClaimInputDestination.ClaimInput));
        var recipient = Recipient(
            recipientId,
            effectiveCertificateCount: 0,
            certificateEvidence: ClaimPreparationEvidenceState.Missing,
            upperLimitEvidence: ClaimPreparationEvidenceState.Missing,
            excludedFromReadinessBlocking: true);

        var result = sut.Evaluate(Context(recipients: [recipient]));

        result.IsReady.Should().BeTrue();
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_still_blocks_zero_certificate_recipient_when_not_excluded()
    {
        var recipientId = Guid.NewGuid();
        var recipient = Recipient(recipientId, effectiveCertificateCount: 0);

        var result = Sut().Evaluate(Context(recipients: [recipient]));

        result.IsReady.Should().BeFalse();
        result.Issues.Should().ContainSingle(issue => issue.RecipientId == recipientId);
    }

    private static ClaimPreparationReadiness Sut(params ClaimInputRequirement[] requirements) =>
        new(new FakeRequirementProvider(requirements));

    private static ClaimInputRequirement Requirement(
        string targetPath,
        ClaimRequirementCondition condition,
        ClaimInputDestination destination = ClaimInputDestination.ClaimInput) =>
        new(targetPath, [$"field:{targetPath}"], condition, destination);

    private static ClaimPreparationContext Context(
        IReadOnlyDictionary<string, ClaimPreparationValue>? officeValues = null,
        IReadOnlyList<ClaimPreparationRecipientContext>? recipients = null,
        ClaimPreparationCalculationEvidence? evidence = null) =>
        new(
            officeValues ?? new Dictionary<string, ClaimPreparationValue>(),
            recipients ?? [],
            evidence ?? Evidence());

    private static ClaimPreparationCalculationEvidence Evidence(
        ClaimPreparationEvidenceState masterVersion = ClaimPreparationEvidenceState.Valid,
        ClaimPreparationEvidenceState averageWage = ClaimPreparationEvidenceState.Valid,
        ClaimPreparationEvidenceState officeProfile = ClaimPreparationEvidenceState.Valid) =>
        new(masterVersion, averageWage, officeProfile);

    private static ClaimPreparationRecipientContext Recipient(
        Guid recipientId,
        IReadOnlyDictionary<string, ClaimPreparationValue>? values = null,
        IReadOnlySet<string>? rowScopes = null,
        int effectiveCertificateCount = 1,
        ClaimPreparationEvidenceState certificateEvidence =
            ClaimPreparationEvidenceState.Valid,
        ClaimPreparationEvidenceState upperLimitEvidence =
            ClaimPreparationEvidenceState.NotApplicable,
        bool excludedFromReadinessBlocking = false) =>
        new(
            recipientId,
            values ?? new Dictionary<string, ClaimPreparationValue>(),
            rowScopes ?? new HashSet<string>(StringComparer.Ordinal),
            effectiveCertificateCount,
            certificateEvidence,
            upperLimitEvidence,
            excludedFromReadinessBlocking);

    private sealed class FakeRequirementProvider(params ClaimInputRequirement[] requirements)
        : IClaimInputRequirementProvider
    {
        public IReadOnlyList<ClaimInputRequirement> GetRequirements() => requirements;
    }
}
