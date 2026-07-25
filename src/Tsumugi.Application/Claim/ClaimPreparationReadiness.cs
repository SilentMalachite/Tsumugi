using Tsumugi.Application.Abstractions;

namespace Tsumugi.Application.Claim;

public sealed class ClaimPreparationReadiness
{
    // Task 9のbuilder群（ClaimPreparationContextBuilder / ClaimCalculationRequestBuilder）が
    // 同一のfield codeでissueを重複なく合流できるよう、正準field codeをinternal共有する。
    internal const string EffectiveCertificateField = "Certificate.Effective";
    internal const string MasterVersionField = "ClaimMaster.Version";
    internal const string AverageWageEvidenceField = "AverageWageAnnualEvidence.Effective";
    internal const string OfficeClaimProfileField = "OfficeClaimProfile.Effective";
    internal const string CertificateEvidenceField = "CertificateClaimEvidence.Effective";
    internal const string OriginalEvidenceField = "CertificateClaimEvidence.Original";
    internal const string UpperLimitStatementField = "UpperLimitManagementStatement.Effective";

    private readonly IClaimInputRequirementProvider _requirementProvider;

    public ClaimPreparationReadiness(IClaimInputRequirementProvider requirementProvider)
    {
        ArgumentNullException.ThrowIfNull(requirementProvider);
        _requirementProvider = requirementProvider;
    }

    public ClaimPreparationResult Evaluate(ClaimPreparationContext context, string specificationVersion)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(specificationVersion);

        var issues = new HashSet<ClaimPreparationIssue>();
        AddGlobalIssues(context, issues);
        foreach (var recipient in context.Recipients)
        {
            // 実績0日かつ有効ClaimInputなしの利用者は請求明細を生成しないため、
            // 証・入力系の必須判定から除外する（一覧には残す。Task 9b）。
            if (recipient.ExcludedFromReadinessBlocking) continue;
            AddRecipientIssues(recipient, issues);
        }

        foreach (var requirement in _requirementProvider.GetRequirements(specificationVersion))
        {
            if (requirement.Destination == ClaimInputDestination.Office)
            {
                ClaimRequirementEvaluator.AddMissingRequirementIssue(
                    requirement,
                    recipientId: null,
                    context.OfficeValues,
                    rowScopes: EmptyRowScopes,
                    issues);
                continue;
            }

            foreach (var recipient in context.Recipients)
            {
                if (recipient.ExcludedFromReadinessBlocking) continue;
                ClaimRequirementEvaluator.AddMissingRequirementIssue(
                    requirement,
                    recipient.RecipientId,
                    recipient.Values,
                    recipient.RowScopes,
                    issues);
            }
        }

        var orderedIssues = issues
            .OrderBy(issue => issue.RecipientId.HasValue)
            .ThenBy(issue => issue.RecipientId)
            .ThenBy(issue => issue.Code)
            .ThenBy(issue => issue.FieldCode, StringComparer.Ordinal)
            .ToArray();
        return new ClaimPreparationResult(orderedIssues.Length == 0, orderedIssues);
    }

    private static IReadOnlySet<string> EmptyRowScopes { get; } =
        new HashSet<string>(StringComparer.Ordinal);

    private static void AddGlobalIssues(
        ClaimPreparationContext context,
        HashSet<ClaimPreparationIssue> issues)
    {
        AddEvidenceIssue(
            context.CalculationEvidence.MasterVersion,
            recipientId: null,
            MasterVersionField,
            ClaimInputDestination.ClaimPreparation,
            issues,
            missingCode: ClaimPreparationIssueCode.MasterVersionUnavailable);
        AddEvidenceIssue(
            context.CalculationEvidence.AverageWageAnnualEvidence,
            recipientId: null,
            AverageWageEvidenceField,
            ClaimInputDestination.ClaimInput,
            issues);
        AddEvidenceIssue(
            context.CalculationEvidence.OfficeClaimProfile,
            recipientId: null,
            OfficeClaimProfileField,
            ClaimInputDestination.ClaimInput,
            issues);
    }

    private static void AddRecipientIssues(
        ClaimPreparationRecipientContext recipient,
        HashSet<ClaimPreparationIssue> issues)
    {
        if (recipient.EffectiveCertificateCount == 0)
        {
            issues.Add(new ClaimPreparationIssue(
                ClaimPreparationIssueCode.MissingRequiredField,
                recipient.RecipientId,
                EffectiveCertificateField,
                ClaimInputDestination.Certificate));
        }
        else if (recipient.EffectiveCertificateCount > 1)
        {
            issues.Add(new ClaimPreparationIssue(
                ClaimPreparationIssueCode.MultipleEffectiveCertificates,
                recipient.RecipientId,
                EffectiveCertificateField,
                ClaimInputDestination.Certificate));
        }

        AddEvidenceIssue(
            recipient.CertificateClaimEvidence,
            recipient.RecipientId,
            CertificateEvidenceField,
            ClaimInputDestination.ClaimInput,
            issues,
            originalFieldCode: OriginalEvidenceField);
        AddEvidenceIssue(
            recipient.UpperLimitManagementStatement,
            recipient.RecipientId,
            UpperLimitStatementField,
            ClaimInputDestination.ClaimInput,
            issues,
            allowNotApplicable: true);
    }

    private static void AddEvidenceIssue(
        ClaimPreparationEvidenceState state,
        Guid? recipientId,
        string fieldCode,
        ClaimInputDestination destination,
        HashSet<ClaimPreparationIssue> issues,
        bool allowNotApplicable = false,
        ClaimPreparationIssueCode missingCode = ClaimPreparationIssueCode.MissingRequiredEvidence,
        string? originalFieldCode = null)
    {
        var code = state switch
        {
            ClaimPreparationEvidenceState.Valid => ClaimPreparationIssueCode.Unknown,
            ClaimPreparationEvidenceState.NotApplicable when allowNotApplicable =>
                ClaimPreparationIssueCode.Unknown,
            ClaimPreparationEvidenceState.Missing => missingCode,
            ClaimPreparationEvidenceState.InvalidHistory =>
                ClaimPreparationIssueCode.InvalidEffectiveHistory,
            ClaimPreparationEvidenceState.OriginalUnconfirmed =>
                ClaimPreparationIssueCode.OriginalEvidenceUnconfirmed,
            ClaimPreparationEvidenceState.SourceMismatch =>
                ClaimPreparationIssueCode.EvidenceSourceMismatch,
            _ => ClaimPreparationIssueCode.UnresolvedEvidence,
        };
        if (code == ClaimPreparationIssueCode.Unknown)
        {
            return;
        }

        issues.Add(new ClaimPreparationIssue(
            code,
            recipientId,
            code == ClaimPreparationIssueCode.OriginalEvidenceUnconfirmed
                ? originalFieldCode ?? fieldCode
                : fieldCode,
            destination));
    }

}
