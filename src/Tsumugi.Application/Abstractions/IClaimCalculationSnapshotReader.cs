using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Abstractions;

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
/// </remarks>
public sealed record ClaimCalculationSnapshot(
    IReadOnlyList<Guid> RecipientIds,
    OfficeClaimProfile? Profile,
    IReadOnlyList<ClaimInput> EffectiveClaimInputs,
    IReadOnlyDictionary<Guid, CertificateClaimEvidence> EffectiveCertificateEvidenceByRecipient,
    IReadOnlyList<AverageWageAnnualEvidence> EffectiveAverageWageEvidences,
    IReadOnlyDictionary<Guid, int> BilledDaysByRecipient,
    IReadOnlyDictionary<Guid, int> EffectiveCertificateCountByRecipient);

/// <summary>
/// 事業所・サービス月のクレーム算定入力を単一の読み取り専用トランザクションで取得する。
/// </summary>
public interface IClaimCalculationSnapshotReader
{
    Task<ClaimCalculationSnapshot> ReadAsync(
        Guid officeId, ServiceMonth serviceMonth, CancellationToken ct);
}
