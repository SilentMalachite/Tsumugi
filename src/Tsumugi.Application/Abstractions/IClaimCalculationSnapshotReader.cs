using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Abstractions;

/// <summary>
/// 利用者・サービス月ごとに縮約したDailyRecordの請求関連属性（Task 9c）。
/// 対象は当月の実効レコード（<c>DailyRecordPolicy.EffectiveByDate</c>で訂正・取消を反映済み）のうち
/// <see cref="Attendance.Present"/>の日のみ（欠席日はサービス提供行を持たないため対象外）。
/// 複数日にまたがる値は次の規則で1利用者1値へ縮約する（読み取り専用の請求準備コンテキストは
/// 日次行を持たないため、代表1件・集計値として扱う。値を捏造せず、実際に入力された日次データの
/// 純粋な縮約であることに注意）。
/// <list type="bullet">
/// <item>真偽値系（<see cref="OffsiteSupportApplied"/> 等）: 当月いずれかの日でtrueならtrue（OR）。</item>
/// <item><see cref="SpecialVisitSupportMinutesTotal"/>: 当月合計（SUM）。</item>
/// <item>時刻・区分系（<see cref="ServiceStartTime"/> 等）: 暦日昇順で最初に値が入力された日を代表とする。</item>
/// </list>
/// 対象日が1件もない利用者は <see cref="Empty"/>（すべて未入力相当の既定値）。
/// </summary>
public sealed record ClaimDailyRecordAggregate(
    TimeOnly? ServiceStartTime,
    TimeOnly? ServiceEndTime,
    int SpecialVisitSupportMinutesTotal,
    bool OffsiteSupportApplied,
    MedicalCoordinationType MedicalCoordinationType,
    TrialUseSupportType TrialUseSupportType,
    bool RegionalCollaborationApplied,
    bool IntensiveSupportApplied,
    bool EmergencyAdmissionApplied)
{
    public static ClaimDailyRecordAggregate Empty { get; } = new(
        ServiceStartTime: null,
        ServiceEndTime: null,
        SpecialVisitSupportMinutesTotal: 0,
        OffsiteSupportApplied: false,
        MedicalCoordinationType: MedicalCoordinationType.Unspecified,
        TrialUseSupportType: TrialUseSupportType.Unspecified,
        RegionalCollaborationApplied: false,
        IntensiveSupportApplied: false,
        EmergencyAdmissionApplied: false);
}

/// <summary>
/// 事業所・サービス月単位で、請求算定に必要な入力の一貫したスナップショット。
/// 各コレクションは追記型revision chainを実効値へ縮約済み（取消・訂正は反映済み）。
/// </summary>
/// <remarks>
/// <para>
/// <see cref="RecipientIds"/> は「当月1日時点で実効な契約」「当月に実効なPresent日次記録が1件以上」
/// 「当月の実効ClaimInput」の3ソースの和集合（Guid昇順で決定論的に整列）。
/// ClaimInput未入力のまま出席実績だけがある利用者を対象範囲から黙って落とさないための設計であり、
/// いずれか1ソースにでも該当すれば対象者として可視化する。
/// </para>
/// <para>
/// <see cref="BilledDaysByRecipient"/> は <see cref="RecipientIds"/> の全員分のエントリを持つ
/// （出席日数0でも省略しない）。
/// </para>
/// <para>
/// <see cref="EffectiveCertificateCountByRecipient"/> は、サービス月の暦日と有効期間が重なる
/// （<c>Overlaps</c>）受給者証rootの数を <see cref="RecipientIds"/> の全員分保持する。
/// <see cref="EffectiveCertificateEvidenceByRecipient"/> は利用者IDを鍵とする明示的な対応付けで、
/// エントリが存在するのは「その利用者の<see cref="EffectiveCertificateCountByRecipient"/>が
/// ちょうど1件、かつその受給者証に実効根拠が存在する」場合に限る（位置対応ではない）。
/// 0件（証なし）・2件以上（月途中の証切替）・根拠未登録はこの層ではエントリを作らず、
/// 件数を通じて可視化するに留める。判定（missing / 複数該当でfail-closed）は
/// 請求readiness gate側の責務とする。
/// </para>
/// <para>
/// （Task 9c追加）<see cref="EffectiveCertificateByRecipient"/> は
/// <see cref="EffectiveCertificateCountByRecipient"/>がちょうど1件のときだけ、その受給者証実体を
/// 利用者IDを鍵として保持する（<see cref="EffectiveCertificateEvidenceByRecipient"/>と同じ「位置対応
/// ではなく明示キー、0件・2件以上はエントリなし」規約）。
/// </para>
/// <para>
/// （Task 9c追加）<see cref="EffectiveContractedProviderByRecipient"/> は、
/// <see cref="EffectiveCertificateByRecipient"/>の受給者証に紐づく「サービス事業者記入欄」のうち、
/// 本事業所（<c>ContractedProvider.ProviderNumber == Office.OfficeNumber</c>）かつサービス月と
/// 契約期間（<c>ContractDate</c>〜<c>TerminationDate</c>）が重なる行を利用者IDを鍵として保持する。
/// 該当が0件または2件以上（契約行の重複）の場合はエントリを作らない（代表を選ばずfail-closed）。
/// </para>
/// <para>
/// （Task 9c追加）<see cref="DailyRecordAggregateByRecipient"/> は
/// <see cref="ClaimDailyRecordAggregate"/>を参照。<see cref="RecipientIds"/>のうち当月に実効な
/// Present日次記録を1件以上持つ利用者のみエントリを持つ（それ以外は
/// <see cref="ClaimDailyRecordAggregate.Empty"/>と等価に扱ってよい）。
/// </para>
/// <para>
/// （Task 9c追加）<see cref="IntensiveSupportEpisodeStartDateByRecipient"/> は、事業所・利用者ごとの
/// <c>IntensiveSupportEpisode</c>追記型revision chainを<c>IntensiveSupportEpisodePolicy.Effective</c>で
/// 縮約した結果、取消でない実効エピソードが存在する利用者だけを鍵として開始日を保持する
/// （取消済み・未登録はエントリなし）。
/// </para>
/// <para>
/// （Task 11追加）<see cref="OfficeCapabilities"/> は事業所の体制届（<c>OfficeCapability</c>）
/// 全レコード（同一読み取りtx）。実効1件の選定（<c>OfficeCapabilityPolicy.Resolve</c>・ADR 0021）と
/// 曖昧時のフェイルクローズは<c>ClaimCalculationRequestBuilder</c>の責務。
/// <c>null</c>（旧snapshot形状）は「体制届未登録」と同義に扱う（production readerは常に非null）。
/// </para>
/// </remarks>
public sealed record ClaimCalculationSnapshot(
    IReadOnlyList<Guid> RecipientIds,
    OfficeClaimProfile? Profile,
    IReadOnlyList<ClaimInput> EffectiveClaimInputs,
    IReadOnlyDictionary<Guid, CertificateClaimEvidence> EffectiveCertificateEvidenceByRecipient,
    IReadOnlyList<AverageWageAnnualEvidence> EffectiveAverageWageEvidences,
    IReadOnlyDictionary<Guid, int> BilledDaysByRecipient,
    IReadOnlyDictionary<Guid, int> EffectiveCertificateCountByRecipient,
    IReadOnlyDictionary<Guid, Certificate>? EffectiveCertificateByRecipient = null,
    IReadOnlyDictionary<Guid, ContractedProvider>? EffectiveContractedProviderByRecipient = null,
    IReadOnlyDictionary<Guid, ClaimDailyRecordAggregate>? DailyRecordAggregateByRecipient = null,
    IReadOnlyDictionary<Guid, DateOnly>? IntensiveSupportEpisodeStartDateByRecipient = null,
    IReadOnlyList<OfficeCapability>? OfficeCapabilities = null);

/// <summary>
/// 事業所・サービス月のクレーム算定入力を単一の読み取り専用トランザクションで取得する。
/// </summary>
public interface IClaimCalculationSnapshotReader
{
    Task<ClaimCalculationSnapshot> ReadAsync(
        Guid officeId, ServiceMonth serviceMonth, CancellationToken ct);
}
