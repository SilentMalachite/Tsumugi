using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Logic.Claim.Models;

namespace Tsumugi.Application.Claim;

/// <summary>純粋写像の結果。builder自身が確定できた不足はIssuesとして返す（推測しない）。</summary>
public sealed record ClaimPreparationContextBuildResult(
    ClaimPreparationContext Context,
    IReadOnlyList<ClaimPreparationIssue> Issues);

/// <summary>
/// <see cref="ClaimCalculationSnapshot"/>から<see cref="ClaimPreparationContext"/>への純粋写像。
/// snapshotが運ばない情報（Certificate請求列・DailyRecord請求列・上限額管理明細書など）は
/// 値を捏造せず欠落のまま残し、readiness gate側でフェイルクローズさせる。
/// </summary>
public static class ClaimPreparationContextBuilder
{
    internal const string OfficeEffectiveField = nameof(Office) + ".Effective";
    internal const string ClaimInputEffectiveField = nameof(ClaimInput) + ".Effective";

    public static ClaimPreparationContextBuildResult Build(
        ClaimCalculationSnapshot snapshot,
        Office? office,
        bool masterVersionAvailable)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var issues = new List<ClaimPreparationIssue>();
        if (office is null)
        {
            issues.Add(new ClaimPreparationIssue(
                ClaimPreparationIssueCode.MissingRequiredField,
                RecipientId: null,
                OfficeEffectiveField,
                ClaimInputDestination.Office));
        }

        var evidenceByRecipient = ClaimSnapshotEvidenceAssociation.Build(snapshot);
        var recipients = snapshot.RecipientIds
            .Select(recipientId => BuildRecipient(snapshot, recipientId, evidenceByRecipient, issues))
            .ToArray();

        var context = new ClaimPreparationContext(
            BuildOfficeValues(office),
            recipients,
            new ClaimPreparationCalculationEvidence(
                MasterVersion: masterVersionAvailable
                    ? ClaimPreparationEvidenceState.Valid
                    : ClaimPreparationEvidenceState.Missing,
                AverageWageAnnualEvidence: snapshot.EffectiveAverageWageEvidences.Count switch
                {
                    1 => ClaimPreparationEvidenceState.Valid,
                    0 => ClaimPreparationEvidenceState.Missing,
                    _ => ClaimPreparationEvidenceState.Unknown,
                },
                OfficeClaimProfile: snapshot.Profile is null
                    ? ClaimPreparationEvidenceState.Missing
                    : ClaimPreparationEvidenceState.Valid));

        return new ClaimPreparationContextBuildResult(context, issues);
    }

    private static Dictionary<string, ClaimPreparationValue> BuildOfficeValues(Office? office)
    {
        if (office is null) return new Dictionary<string, ClaimPreparationValue>(StringComparer.Ordinal);

        return new Dictionary<string, ClaimPreparationValue>(StringComparer.Ordinal)
        {
            [Path(nameof(Office), nameof(Office.PostalCode))] = TextOrNotApplicable(office.PostalCode),
            [Path(nameof(Office), nameof(Office.Address))] = TextOrNotApplicable(office.Address),
            [Path(nameof(Office), nameof(Office.PhoneNumber))] = TextOrNotApplicable(office.PhoneNumber),
            [Path(nameof(Office), nameof(Office.RepresentativeTitleAndName))] =
                TextOrNotApplicable(office.RepresentativeTitleAndName),
        };
    }

    private static ClaimPreparationRecipientContext BuildRecipient(
        ClaimCalculationSnapshot snapshot,
        Guid recipientId,
        IReadOnlyDictionary<Guid, CertificateClaimEvidence> evidenceByRecipient,
        List<ClaimPreparationIssue> issues)
    {
        var inputs = snapshot.EffectiveClaimInputs
            .Where(input => input.RecipientId == recipientId)
            .ToArray();
        if (inputs.Length != 1)
        {
            issues.Add(new ClaimPreparationIssue(
                inputs.Length == 0
                    ? ClaimPreparationIssueCode.MissingRequiredField
                    : ClaimPreparationIssueCode.InvalidEffectiveHistory,
                recipientId,
                ClaimInputEffectiveField,
                ClaimInputDestination.ClaimInput));
        }

        var input = inputs.Length == 1 ? inputs[0] : null;
        var certificateCount = snapshot.EffectiveCertificateCountByRecipient
            .GetValueOrDefault(recipientId);
        var evidence = evidenceByRecipient.GetValueOrDefault(recipientId);

        return new ClaimPreparationRecipientContext(
            recipientId,
            BuildRecipientValues(input),
            rowScopes: new HashSet<string>(StringComparer.Ordinal),
            certificateCount,
            CertificateEvidenceState(certificateCount, evidence),
            StatementState(evidence));
    }

    private static Dictionary<string, ClaimPreparationValue> BuildRecipientValues(
        ClaimInput? input)
        => new Dictionary<string, ClaimPreparationValue>(StringComparer.Ordinal)
        {
            [Path(nameof(ClaimInput), nameof(ClaimInput.UpperLimitManagementResult))] =
                input?.UpperLimitManagementResult is { } result
                    ? ClaimPreparationValue.Code(result.ToString())
                    : ClaimPreparationValue.NotApplicable(),
            [Path(nameof(ClaimInput), nameof(ClaimInput.UpperLimitManagedAmountYen))] =
                NumberOrNotApplicable(input?.UpperLimitManagedAmountYen),
            [Path(nameof(ClaimInput), nameof(ClaimInput.MunicipalSubsidyAmountYen))] =
                NumberOrNotApplicable(input?.MunicipalSubsidyAmountYen),
            [Path(nameof(ClaimInput), nameof(ClaimInput.ExceptionalUsageStartMonth))] =
                MonthOrNotApplicable(input?.ExceptionalUsageStartMonth),
            [Path(nameof(ClaimInput), nameof(ClaimInput.ExceptionalUsageEndMonth))] =
                MonthOrNotApplicable(input?.ExceptionalUsageEndMonth),
            [Path(nameof(ClaimInput), nameof(ClaimInput.ExceptionalUsageDays))] =
                NumberOrNotApplicable(input?.ExceptionalUsageDays),
            [Path(nameof(ClaimInput), nameof(ClaimInput.StandardUsageDayTotal))] =
                NumberOrNotApplicable(input?.StandardUsageDayTotal),
        };

    internal static ClaimPreparationEvidenceState CertificateEvidenceState(
        int certificateCount, CertificateClaimEvidence? evidence)
        => certificateCount switch
        {
            // 証0件はMissing（証自体の欠落はEffectiveCertificateCount経由で別途issue化される）。
            0 => ClaimPreparationEvidenceState.Missing,
            1 when evidence is null => ClaimPreparationEvidenceState.Missing,
            1 when !evidence.MonthlyCostCap.IsEntered
                || evidence.MonthlyCostCap.ValueYen is null => ClaimPreparationEvidenceState.Missing,
            1 when string.IsNullOrWhiteSpace(evidence.OriginalDocumentReference)
                || evidence.ConfirmedAt is null
                || string.IsNullOrWhiteSpace(evidence.ConfirmedBy) =>
                ClaimPreparationEvidenceState.OriginalUnconfirmed,
            1 => ClaimPreparationEvidenceState.Valid,
            // 2件以上（月途中の証切替）は代表を選ばずUnknown（readinessがUnresolvedEvidence化）。
            _ => ClaimPreparationEvidenceState.Unknown,
        };

    private static ClaimPreparationEvidenceState StatementState(CertificateClaimEvidence? evidence)
        => evidence?.UpperLimitManagementApplicability switch
        {
            UpperLimitManagementApplicability.NotApplicable => ClaimPreparationEvidenceState.NotApplicable,
            // snapshotは上限額管理明細書を運ばない（将来スライス）。管理対象は明細書必須なので欠落扱い。
            UpperLimitManagementApplicability.Applicable => ClaimPreparationEvidenceState.Missing,
            _ => ClaimPreparationEvidenceState.Unknown,
        };

    private static string Path(string model, string property) => model + "." + property;

    private static ClaimPreparationValue TextOrNotApplicable(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? ClaimPreparationValue.NotApplicable()
            : ClaimPreparationValue.Text(value);

    private static ClaimPreparationValue NumberOrNotApplicable(int? value)
        => value is { } number
            ? ClaimPreparationValue.Number(number)
            : ClaimPreparationValue.NotApplicable();

    private static ClaimPreparationValue MonthOrNotApplicable(
        Domain.ValueObjects.ServiceMonth? value)
        => value is { } month
            ? ClaimPreparationValue.Code(month.ToString())
            : ClaimPreparationValue.NotApplicable();
}

/// <summary>
/// snapshotのcertificate根拠を利用者へ対応付ける。<c>CertificateClaimEvidence</c>は
/// 利用者IDを持たないため、readerの構築順序（<c>RecipientIds</c>昇順に、証がちょうど1件の
/// 利用者の実効根拠だけを追加する）に基づく位置対応で復元する。件数が一致しない場合は
/// どの利用者の根拠かを確定できないため、対応付けを全面的に放棄してフェイルクローズする
/// （黙って誤対応させない）。
/// </summary>
internal static class ClaimSnapshotEvidenceAssociation
{
    internal static IReadOnlyDictionary<Guid, CertificateClaimEvidence> Build(
        ClaimCalculationSnapshot snapshot)
    {
        var singleCertificateRecipients = snapshot.RecipientIds
            .Where(recipientId =>
                snapshot.EffectiveCertificateCountByRecipient.GetValueOrDefault(recipientId) == 1)
            .ToArray();
        if (snapshot.EffectiveCertificateEvidences.Count != singleCertificateRecipients.Length)
        {
            return new Dictionary<Guid, CertificateClaimEvidence>();
        }

        return singleCertificateRecipients
            .Zip(snapshot.EffectiveCertificateEvidences)
            .ToDictionary(pair => pair.First, pair => pair.Second);
    }
}
