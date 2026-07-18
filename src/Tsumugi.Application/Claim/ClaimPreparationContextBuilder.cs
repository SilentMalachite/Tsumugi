using System.Globalization;
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

        var evidenceByRecipient = snapshot.EffectiveCertificateEvidenceByRecipient;
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
        var billedDays = snapshot.BilledDaysByRecipient.GetValueOrDefault(recipientId);
        // 実績0日かつ有効ClaimInputなしの利用者は当月請求明細を生成しないため
        // （ClaimCalculationRequestBuilder.BuildSourcesと同じ判定）、readinessの
        // ブロック評価から除外する（一覧には残す。Task 9b）。履歴不整合（2件以上）は
        // 実績日数に関わらず可視化を続ける。
        var excludedFromReadinessBlocking = billedDays == 0 && inputs.Length == 0;
        if (inputs.Length != 1 && !excludedFromReadinessBlocking)
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

        // Task 9c: Certificate.* / ContractedProvider.* / DailyRecord.* / IntensiveSupportEpisode.StartDate
        // の写像元。証・契約行・日次実績いずれも「未入力/該当なし」はNotApplicableとして明示し、
        // Unresolved（値そのものがValues辞書に無い状態）を作らない（readiness engineの自己参照条件が
        // Unresolvedになるとfail-closedが解除不能になるため）。
        var certificate = snapshot.EffectiveCertificateByRecipient?.GetValueOrDefault(recipientId);
        var contractedProvider = snapshot.EffectiveContractedProviderByRecipient?.GetValueOrDefault(recipientId);
        var dailyRecordAggregate = snapshot.DailyRecordAggregateByRecipient?.GetValueOrDefault(recipientId)
            ?? ClaimDailyRecordAggregate.Empty;
        DateOnly? intensiveSupportEpisodeStartDate =
            snapshot.IntensiveSupportEpisodeStartDateByRecipient is { } startDates
                && startDates.TryGetValue(recipientId, out var startDate)
                ? startDate
                : null;

        return new ClaimPreparationRecipientContext(
            recipientId,
            BuildRecipientValues(
                input, certificate, contractedProvider, dailyRecordAggregate, intensiveSupportEpisodeStartDate),
            rowScopes: new HashSet<string>(StringComparer.Ordinal),
            certificateCount,
            CertificateEvidenceState(certificateCount, evidence),
            StatementState(evidence),
            excludedFromReadinessBlocking);
    }

    private static Dictionary<string, ClaimPreparationValue> BuildRecipientValues(
        ClaimInput? input,
        Certificate? certificate,
        ContractedProvider? contractedProvider,
        ClaimDailyRecordAggregate dailyRecordAggregate,
        DateOnly? intensiveSupportEpisodeStartDate)
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

            // Certificate.*（Task 9c）。MunicipalityNumberは常時必須（always）、他の2件は
            // 自己参照modelPresent（値がある時だけその値自体が要求を満たす＝実質「入力するなら
            // 空にしない」程度の意味）。
            [Path(nameof(Certificate), nameof(Certificate.MunicipalityNumber))] =
                TextOrNotApplicable(certificate?.MunicipalityNumber),
            [Path(nameof(Certificate), nameof(Certificate.SubsidyMunicipalityNumber))] =
                TextOrNotApplicable(certificate?.SubsidyMunicipalityNumber),
            [Path(nameof(Certificate), nameof(Certificate.UpperLimitManagementProviderNumber))] =
                TextOrNotApplicable(certificate?.UpperLimitManagementProviderNumber),

            // ContractedProvider.CertificateEntryNumber（Task 9c）。常時必須（always）。
            [Path(nameof(ContractedProvider), nameof(ContractedProvider.CertificateEntryNumber))] =
                NumberOrNotApplicable(contractedProvider?.CertificateEntryNumber),

            // DailyRecord.*（Task 9c）。いずれも自己参照条件（modelPresent/modelNonZero/modelTrue/
            // modelIn）で、当月の実効Present日次記録から縮約した代表値を渡す
            // （ClaimDailyRecordAggregateの縮約規則はそのdoc-comment参照）。
            [Path(nameof(DailyRecord), nameof(DailyRecord.ServiceStartTime))] =
                TimeOrNotApplicable(dailyRecordAggregate.ServiceStartTime),
            [Path(nameof(DailyRecord), nameof(DailyRecord.ServiceEndTime))] =
                TimeOrNotApplicable(dailyRecordAggregate.ServiceEndTime),
            [Path(nameof(DailyRecord), nameof(DailyRecord.SpecialVisitSupportMinutes))] =
                ClaimPreparationValue.Number(dailyRecordAggregate.SpecialVisitSupportMinutesTotal),
            [Path(nameof(DailyRecord), nameof(DailyRecord.OffsiteSupportApplied))] =
                ClaimPreparationValue.Boolean(dailyRecordAggregate.OffsiteSupportApplied),
            [Path(nameof(DailyRecord), nameof(DailyRecord.MedicalCoordinationType))] =
                ClaimPreparationValue.Code(dailyRecordAggregate.MedicalCoordinationType.ToString()),
            [Path(nameof(DailyRecord), nameof(DailyRecord.TrialUseSupportType))] =
                ClaimPreparationValue.Code(dailyRecordAggregate.TrialUseSupportType.ToString()),
            [Path(nameof(DailyRecord), nameof(DailyRecord.RegionalCollaborationApplied))] =
                ClaimPreparationValue.Boolean(dailyRecordAggregate.RegionalCollaborationApplied),
            [Path(nameof(DailyRecord), nameof(DailyRecord.IntensiveSupportApplied))] =
                ClaimPreparationValue.Boolean(dailyRecordAggregate.IntensiveSupportApplied),
            [Path(nameof(DailyRecord), nameof(DailyRecord.EmergencyAdmissionApplied))] =
                ClaimPreparationValue.Boolean(dailyRecordAggregate.EmergencyAdmissionApplied),

            // IntensiveSupportEpisode.StartDate（Task 9c）。自己参照modelPresent。
            [Path(nameof(IntensiveSupportEpisode), nameof(IntensiveSupportEpisode.StartDate))] =
                DateOrNotApplicable(intensiveSupportEpisodeStartDate),
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

    private static ClaimPreparationValue TimeOrNotApplicable(TimeOnly? value)
        => value is { } time
            ? ClaimPreparationValue.Text(time.ToString("HH:mm", CultureInfo.InvariantCulture))
            : ClaimPreparationValue.NotApplicable();

    private static ClaimPreparationValue DateOrNotApplicable(DateOnly? value)
        => value is { } date
            ? ClaimPreparationValue.Date(date)
            : ClaimPreparationValue.NotApplicable();
}
