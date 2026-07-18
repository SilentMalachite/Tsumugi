using System.Collections.ObjectModel;

namespace Tsumugi.Application.Claim;

public enum ClaimPreparationIssueCode
{
    Unknown = 0,
    MissingRequiredField = 1,
    MultipleEffectiveCertificates = 2,
    InvalidEffectiveHistory = 3,
    MasterVersionUnavailable = 4,
    OriginalEvidenceUnconfirmed = 5,
    UnresolvedRequirementCondition = 6,
    MissingRequiredEvidence = 7,
    EvidenceSourceMismatch = 8,
    UnresolvedEvidence = 9,
}

public enum ClaimPreparationValueKind
{
    Unknown = 0,
    Text = 1,
    Number = 2,
    Boolean = 3,
    Code = 4,
    Date = 5,
    NotApplicable = 6,
}

public enum ClaimPreparationEvidenceState
{
    Unknown = 0,
    Valid = 1,
    NotApplicable = 2,
    Missing = 3,
    InvalidHistory = 4,
    OriginalUnconfirmed = 5,
    SourceMismatch = 6,
}

public sealed record ClaimPreparationValue
{
    private ClaimPreparationValue(
        ClaimPreparationValueKind kind,
        string? stringValue = null,
        decimal? numberValue = null,
        bool? booleanValue = null,
        DateOnly? dateValue = null)
    {
        Kind = kind;
        StringValue = stringValue;
        NumberValue = numberValue;
        BooleanValue = booleanValue;
        DateValue = dateValue;
    }

    public ClaimPreparationValueKind Kind { get; }
    public string? StringValue { get; }
    public decimal? NumberValue { get; }
    public bool? BooleanValue { get; }
    public DateOnly? DateValue { get; }

    public static ClaimPreparationValue Text(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new(ClaimPreparationValueKind.Text, stringValue: value);
    }

    public static ClaimPreparationValue Number(decimal value) =>
        new(ClaimPreparationValueKind.Number, numberValue: value);

    public static ClaimPreparationValue Boolean(bool value) =>
        new(ClaimPreparationValueKind.Boolean, booleanValue: value);

    public static ClaimPreparationValue Code(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new(ClaimPreparationValueKind.Code, stringValue: value);
    }

    public static ClaimPreparationValue Date(DateOnly value) =>
        new(ClaimPreparationValueKind.Date, dateValue: value);

    public static ClaimPreparationValue NotApplicable() =>
        new(ClaimPreparationValueKind.NotApplicable);
}

public sealed record ClaimPreparationRecipientContext
{
    public ClaimPreparationRecipientContext(
        Guid recipientId,
        IReadOnlyDictionary<string, ClaimPreparationValue> values,
        IReadOnlySet<string> rowScopes,
        int effectiveCertificateCount,
        ClaimPreparationEvidenceState certificateClaimEvidence,
        ClaimPreparationEvidenceState upperLimitManagementStatement,
        bool excludedFromReadinessBlocking = false)
    {
        if (recipientId == Guid.Empty)
        {
            throw new ArgumentException("Recipient ID must not be empty.", nameof(recipientId));
        }

        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(rowScopes);
        ArgumentOutOfRangeException.ThrowIfNegative(effectiveCertificateCount);

        RecipientId = recipientId;
        Values = CopyValues(values);
        RowScopes = new HashSet<string>(rowScopes, StringComparer.Ordinal);
        EffectiveCertificateCount = effectiveCertificateCount;
        CertificateClaimEvidence = certificateClaimEvidence;
        UpperLimitManagementStatement = upperLimitManagementStatement;
        ExcludedFromReadinessBlocking = excludedFromReadinessBlocking;
    }

    public Guid RecipientId { get; }
    public IReadOnlyDictionary<string, ClaimPreparationValue> Values { get; }
    public IReadOnlySet<string> RowScopes { get; }
    public int EffectiveCertificateCount { get; }
    public ClaimPreparationEvidenceState CertificateClaimEvidence { get; }
    public ClaimPreparationEvidenceState UpperLimitManagementStatement { get; }

    /// <summary>
    /// <c>true</c>のとき、この利用者は実績0日かつ有効ClaimInputなしのため当月の請求明細を
    /// 生成しない（<c>ClaimCalculationRequestBuilder.BuildSources</c>と同じ判定）。
    /// readinessのブロック評価（証・入力系issueと必須requirement）から除外するが、
    /// context自体には残る（一覧表示のため可視）。Task 9b。
    /// </summary>
    public bool ExcludedFromReadinessBlocking { get; }

    private static ReadOnlyDictionary<string, ClaimPreparationValue> CopyValues(
        IReadOnlyDictionary<string, ClaimPreparationValue> values) =>
        new(new Dictionary<string, ClaimPreparationValue>(values, StringComparer.Ordinal));
}

public sealed record ClaimPreparationCalculationEvidence(
    ClaimPreparationEvidenceState MasterVersion,
    ClaimPreparationEvidenceState AverageWageAnnualEvidence,
    ClaimPreparationEvidenceState OfficeClaimProfile);

public sealed record ClaimPreparationContext
{
    public ClaimPreparationContext(
        IReadOnlyDictionary<string, ClaimPreparationValue> officeValues,
        IReadOnlyList<ClaimPreparationRecipientContext> recipients,
        ClaimPreparationCalculationEvidence calculationEvidence)
    {
        ArgumentNullException.ThrowIfNull(officeValues);
        ArgumentNullException.ThrowIfNull(recipients);
        ArgumentNullException.ThrowIfNull(calculationEvidence);
        if (recipients.Any(recipient => recipient is null))
        {
            throw new ArgumentException("Recipients must not contain null.", nameof(recipients));
        }

        OfficeValues = new ReadOnlyDictionary<string, ClaimPreparationValue>(
            new Dictionary<string, ClaimPreparationValue>(officeValues, StringComparer.Ordinal));
        Recipients = Array.AsReadOnly(recipients.ToArray());
        CalculationEvidence = calculationEvidence;
    }

    public IReadOnlyDictionary<string, ClaimPreparationValue> OfficeValues { get; }
    public IReadOnlyList<ClaimPreparationRecipientContext> Recipients { get; }
    public ClaimPreparationCalculationEvidence CalculationEvidence { get; }
}

public sealed record ClaimPreparationIssue(
    ClaimPreparationIssueCode Code,
    Guid? RecipientId,
    string FieldCode,
    ClaimInputDestination Destination);

public sealed record ClaimPreparationResult
{
    public ClaimPreparationResult(bool isReady, IEnumerable<ClaimPreparationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);
        IsReady = isReady;
        Issues = Array.AsReadOnly(issues.ToArray());
    }

    public bool IsReady { get; }
    public IReadOnlyList<ClaimPreparationIssue> Issues { get; }
}
