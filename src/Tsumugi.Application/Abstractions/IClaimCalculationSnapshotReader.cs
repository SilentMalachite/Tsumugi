using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Abstractions;

/// <summary>
/// 事業所・サービス月単位で、請求算定に必要な入力の一貫したスナップショット。
/// 各コレクションは追記型revision chainを実効値へ縮約済み（取消・訂正は反映済み）。
/// </summary>
public sealed record ClaimCalculationSnapshot(
    OfficeClaimProfile? Profile,
    IReadOnlyList<ClaimInput> EffectiveClaimInputs,
    IReadOnlyList<CertificateClaimEvidence> EffectiveCertificateEvidences,
    IReadOnlyList<AverageWageAnnualEvidence> EffectiveAverageWageEvidences,
    IReadOnlyDictionary<Guid, int> BilledDaysByRecipient);

/// <summary>
/// 事業所・サービス月のクレーム算定入力を単一の読み取り専用トランザクションで取得する。
/// </summary>
public interface IClaimCalculationSnapshotReader
{
    Task<ClaimCalculationSnapshot> ReadAsync(
        Guid officeId, ServiceMonth serviceMonth, CancellationToken ct);
}
