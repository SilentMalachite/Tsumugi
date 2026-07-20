using System.Globalization;
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
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

    // report-field-mapping-r8-06.json の rowPresent(service-performance.daily) が参照する行スコープ。
    // 実績記録票の日次行は当月に実効Present日が1件以上ある利用者だけに存在する
    // （BilledDaysByRecipientと同じ実効化走査由来。Task 4 fix round: 元々rowScopesが常に空集合
    // だったため、この行スコープを参照するreadiness ruleが恒久的にNotApplicableへ縮退し、
    // ServiceStartTime/ServiceEndTime/RecipientConfirmationの欠落を検出できなかった）。
    internal const string DailyRecordRowScope = "service-performance.daily";

    // report-field-mapping-r8-06.json の rowPresent(service-performance.intensive-support) が参照する
    // 行スコープ（Phase 3-2 Task 8）。IntensiveSupportEpisode.StartDateはspec §10により「対象月に
    // DailyRecord.IntensiveSupportApplied=trueの日があるときのみ」必須。dailyRecordAggregate.
    // IntensiveSupportApplied（当月の実効Present日をOR縮約した値。ClaimDailyRecordAggregateの
    // doc-comment参照）が true のときだけこのスコープをApplyさせる。requiredCondition側は
    // 元の自己参照modelPresent(IntensiveSupportEpisode.StartDate)を捨ててこのrowPresent単独条件に
    // 置換した（daily:004/005/016と同じTask 4 fixパターン。all(rowPresent(...);modelPresent(...))には
    // しない — All()はNotApplicable優勢のため、StartDate未入力時に自己参照modelPresent legが
    // NotApplicableへ縮退し、rowPresent側のApplyを打ち消してしまい、修正前と同型のfail-openバグを
    // 別形で再現するため）。
    internal const string IntensiveSupportRowScope = "service-performance.intensive-support";

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

        // billedDaysは実効Present日数（本メソッド冒頭で取得済み）。1日以上あれば実績記録票の
        // 日次行が存在するとみなし、rowPresent(service-performance.daily)をApplyさせる。
        var rowScopes = billedDays > 0
            ? new HashSet<string>([DailyRecordRowScope], StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        // Phase 3-2 Task 8: 当月いずれかの実効Present日でIntensiveSupportApplied=trueなら
        // rowPresent(service-performance.intensive-support)をApplyさせる（IntensiveSupportRowScope
        // のdoc-comment参照）。
        if (dailyRecordAggregate.IntensiveSupportApplied)
        {
            rowScopes.Add(IntensiveSupportRowScope);
        }

        return new ClaimPreparationRecipientContext(
            recipientId,
            BuildRecipientValues(
                input, certificate, contractedProvider, dailyRecordAggregate, intensiveSupportEpisodeStartDate),
            rowScopes,
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
            // MunicipalSubsidyAmountYen（Phase 3-2 Task 7）。自己参照レグ
            // （report:benefit-claim-detail:summary:015、modelPresent(ClaimInput.MunicipalSubsidyAmountYen)）
            // 単体は恒久的にfail-open。field-mapping-r7-10.jsonのprovider:J121:04:025
            // （requiredCondition=modelPresent(Certificate.SubsidyMunicipalityNumber)、非自己参照）が
            // 同一TargetPathへ合流し、ClaimInputRequirementProvider.CreateRequirementがAny(...)へ
            // ラップすることで、Certificate.SubsidyMunicipalityNumberが非nullのときこのフィールドを
            // fail-closedで必須化する（下記Certificate.SubsidyMunicipalityNumberコメントで説明する
            // UpperLimitManagement系と同型のAny-mergeクロスフィールドゲート）。回帰は
            // ClaimInputRequirementProviderTests.Provider_combines_municipal_subsidy_cross_field_condition_via_any
            // と ClaimPreviewProductionWiringTests.Real_embedded_requirement_provider_requires_municipal_subsidy_amount_*
            // で固定済み。
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
            //
            // Phase 3-2 Task 5 review: SubsidyMunicipalityNumber / UpperLimitManagementProviderNumber
            // 自身の自己参照条件は、値が無ければ即NotApplicable・値があれば即IsPresent一致で、
            // AddMissingRequirementIssueに到達しない（永久にfail-open）。これはバグではなく仕様
            // （spec §10: 両者とも任意、null許容）に一致した意図的な設計。ただし
            // UpperLimitManagementProviderNumberが非nullのとき ClaimInput.UpperLimitManagementResult /
            // UpperLimitManagedAmountYen を必須化する（spec §10）というクロスフィールド規則は別途、
            // report-field-mapping-r8-06.jsonのupper-limit-management:003/004
            // （requiredCondition=modelPresent(Certificate.UpperLimitManagementProviderNumber)、
            // targetはClaimInput側で非自己参照）＋field-mapping-r7-10.jsonのprovider:J121:01:016/017
            // （自己参照レグ）が同一TargetPathへ合流し、ClaimInputRequirementProvider.CreateRequirementが
            // Any(...)へラップすることで実現されている。この2フィールド自身を「必須化」したい場合は、
            // Certificate.UpperLimitManagementProviderNumberの自己参照条件ではなく、この既存の
            // クロスフィールドAny合流の方を見ること（回帰は
            // ClaimPreviewProductionWiringTests.Real_embedded_requirement_provider_requires_upper_limit_*
            // で固定済み）。
            //
            // Phase 3-2 Task 7: SubsidyMunicipalityNumberも同型のクロスフィールド起点である。
            // 非nullのとき ClaimInput.MunicipalSubsidyAmountYen を必須化する規則が、
            // field-mapping-r7-10.jsonのprovider:J121:04:025
            // （requiredCondition=modelPresent(Certificate.SubsidyMunicipalityNumber)、非自己参照）＋
            // report-field-mapping-r8-06.jsonのsummary:015（自己参照レグ）の合流で実現されている
            // （上のClaimInput.MunicipalSubsidyAmountYenコメント参照。回帰は
            // ClaimPreviewProductionWiringTests.Real_embedded_requirement_provider_requires_municipal_subsidy_amount_*
            // で固定済み）。
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
            // RecipientConfirmation（Task 4 fix round）: rowPresent(service-performance.daily)単独条件
            // （自己参照ではない）で判定するため、他のDailyRecord.*と異なりUnspecifiedはNotApplicable
            // として表現する（Code("Unspecified")にすると常にIsPresent=trueになり、未確認のまま
            // 一生issueが立たなくなるため）。
            [Path(nameof(DailyRecord), nameof(DailyRecord.RecipientConfirmation))] =
                dailyRecordAggregate.RecipientConfirmation == RecipientConfirmationStatus.Unspecified
                    ? ClaimPreparationValue.NotApplicable()
                    : ClaimPreparationValue.Code(dailyRecordAggregate.RecipientConfirmation.ToString()),

            // IntensiveSupportEpisode.StartDate（Task 9c）。Phase 3-2 Task 8 fix: requiredConditionは
            // rowPresent(service-performance.intensive-support)単独条件（自己参照ではない、
            // IntensiveSupportRowScopeのdoc-comment参照）で判定するため、他のDailyRecord.*と異なり
            // 値そのものはUnspecified/NotApplicableを気にせず素直にDateOrNotApplicableへ渡してよい
            // （rowScope側が「必須な状況か否か」を、この値自体が「値あり/なし」を判定する二段構造）。
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
