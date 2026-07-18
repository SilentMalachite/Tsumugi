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

    public ClaimPreparationResult Evaluate(ClaimPreparationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var issues = new HashSet<ClaimPreparationIssue>();
        AddGlobalIssues(context, issues);
        foreach (var recipient in context.Recipients)
        {
            AddRecipientIssues(recipient, issues);
        }

        foreach (var requirement in _requirementProvider.GetRequirements())
        {
            if (requirement.Destination == ClaimInputDestination.Office)
            {
                AddMissingRequirementIssue(
                    requirement,
                    recipientId: null,
                    context.OfficeValues,
                    rowScopes: EmptyRowScopes,
                    issues);
                continue;
            }

            foreach (var recipient in context.Recipients)
            {
                AddMissingRequirementIssue(
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

    private static void AddMissingRequirementIssue(
        ClaimInputRequirement requirement,
        Guid? recipientId,
        IReadOnlyDictionary<string, ClaimPreparationValue> values,
        IReadOnlySet<string> rowScopes,
        HashSet<ClaimPreparationIssue> issues)
    {
        var condition = EvaluateCondition(requirement.Condition, values, rowScopes);
        if (condition == ConditionEvaluation.NotApplicable
            || (condition == ConditionEvaluation.Applies
                && IsPresent(requirement.TargetPath, values)))
        {
            return;
        }

        issues.Add(new ClaimPreparationIssue(
            condition == ConditionEvaluation.Unresolved
                ? ClaimPreparationIssueCode.UnresolvedRequirementCondition
                : ClaimPreparationIssueCode.MissingRequiredField,
            recipientId,
            requirement.TargetPath,
            requirement.Destination));
    }

    private static ConditionEvaluation EvaluateCondition(
        ClaimRequirementCondition condition,
        IReadOnlyDictionary<string, ClaimPreparationValue> values,
        IReadOnlySet<string> rowScopes) =>
        condition switch
        {
            ClaimRequirementCondition.Always => ConditionEvaluation.Applies,
            ClaimRequirementCondition.ModelPresent present =>
                EvaluatePresent(present.ModelPath, values),
            ClaimRequirementCondition.ModelNonZero nonZero =>
                EvaluateNonZero(nonZero.ModelPath, values),
            ClaimRequirementCondition.ModelTrue modelTrue =>
                EvaluateTrue(modelTrue.ModelPath, values),
            ClaimRequirementCondition.RowPresent rowPresent =>
                rowScopes.Contains(rowPresent.RowScope)
                    ? ConditionEvaluation.Applies
                    : ConditionEvaluation.NotApplicable,
            ClaimRequirementCondition.ModelIn modelIn =>
                EvaluateIn(modelIn, values),
            ClaimRequirementCondition.All all =>
                EvaluateAll(all, values, rowScopes),
            ClaimRequirementCondition.Any any =>
                EvaluateAny(any, values, rowScopes),
            _ => throw new InvalidOperationException("Unsupported claim requirement condition."),
        };

    private static ConditionEvaluation EvaluatePresent(
        string path,
        IReadOnlyDictionary<string, ClaimPreparationValue> values)
    {
        if (!values.TryGetValue(path, out var value))
        {
            return ConditionEvaluation.Unresolved;
        }

        return value.Kind == ClaimPreparationValueKind.NotApplicable
            ? ConditionEvaluation.NotApplicable
            : ConditionEvaluation.Applies;
    }

    private static ConditionEvaluation EvaluateNonZero(
        string path,
        IReadOnlyDictionary<string, ClaimPreparationValue> values)
    {
        if (!values.TryGetValue(path, out var value))
        {
            return ConditionEvaluation.Unresolved;
        }

        if (value.Kind == ClaimPreparationValueKind.NotApplicable)
        {
            return ConditionEvaluation.NotApplicable;
        }

        if (value.Kind != ClaimPreparationValueKind.Number)
        {
            return ConditionEvaluation.Unresolved;
        }

        return value.NumberValue != 0
            ? ConditionEvaluation.Applies
            : ConditionEvaluation.NotApplicable;
    }

    private static ConditionEvaluation EvaluateTrue(
        string path,
        IReadOnlyDictionary<string, ClaimPreparationValue> values)
    {
        if (!values.TryGetValue(path, out var value))
        {
            return ConditionEvaluation.Unresolved;
        }

        if (value.Kind == ClaimPreparationValueKind.NotApplicable)
        {
            return ConditionEvaluation.NotApplicable;
        }

        if (value.Kind != ClaimPreparationValueKind.Boolean)
        {
            return ConditionEvaluation.Unresolved;
        }

        return value.BooleanValue == true
            ? ConditionEvaluation.Applies
            : ConditionEvaluation.NotApplicable;
    }

    private static ConditionEvaluation EvaluateIn(
        ClaimRequirementCondition.ModelIn condition,
        IReadOnlyDictionary<string, ClaimPreparationValue> values)
    {
        if (!values.TryGetValue(condition.ModelPath, out var value))
        {
            return ConditionEvaluation.Unresolved;
        }

        if (value.Kind == ClaimPreparationValueKind.NotApplicable)
        {
            return ConditionEvaluation.NotApplicable;
        }

        if ((value.Kind != ClaimPreparationValueKind.Code
                && value.Kind != ClaimPreparationValueKind.Text)
            || value.StringValue is null)
        {
            return ConditionEvaluation.Unresolved;
        }

        return condition.AllowedValues.Contains(value.StringValue, StringComparer.Ordinal)
            ? ConditionEvaluation.Applies
            : ConditionEvaluation.NotApplicable;
    }

    private static ConditionEvaluation EvaluateAll(
        ClaimRequirementCondition.All condition,
        IReadOnlyDictionary<string, ClaimPreparationValue> values,
        IReadOnlySet<string> rowScopes)
    {
        var evaluations = condition.Conditions
            .Select(child => EvaluateCondition(child, values, rowScopes))
            .ToArray();
        if (evaluations.Contains(ConditionEvaluation.NotApplicable))
        {
            return ConditionEvaluation.NotApplicable;
        }

        return evaluations.Contains(ConditionEvaluation.Unresolved)
            ? ConditionEvaluation.Unresolved
            : ConditionEvaluation.Applies;
    }

    private static ConditionEvaluation EvaluateAny(
        ClaimRequirementCondition.Any condition,
        IReadOnlyDictionary<string, ClaimPreparationValue> values,
        IReadOnlySet<string> rowScopes)
    {
        var evaluations = condition.Conditions
            .Select(child => EvaluateCondition(child, values, rowScopes))
            .ToArray();
        if (evaluations.Contains(ConditionEvaluation.Applies))
        {
            return ConditionEvaluation.Applies;
        }

        return evaluations.Contains(ConditionEvaluation.Unresolved)
            ? ConditionEvaluation.Unresolved
            : ConditionEvaluation.NotApplicable;
    }

    private static bool IsPresent(
        string path,
        IReadOnlyDictionary<string, ClaimPreparationValue> values) =>
        values.TryGetValue(path, out var value)
        && value.Kind is not ClaimPreparationValueKind.Unknown
            and not ClaimPreparationValueKind.NotApplicable;

    private enum ConditionEvaluation
    {
        NotApplicable = 0,
        Applies = 1,
        Unresolved = 2,
    }
}
