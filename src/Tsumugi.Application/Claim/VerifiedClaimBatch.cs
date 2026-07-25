using Tsumugi.Domain.Entities;

namespace Tsumugi.Application.Claim;

/// <summary>
/// <see cref="ClaimHistoryVerifier"/> が検証した「実効 revision」。国保連へ渡す成果物
/// （請求CSV・3帳票）はこの型からしか作らない。
/// </summary>
/// <remarks>
/// <para>
/// コンストラクタは private、生成は同一 assembly の internal factory のみ。
/// <c>ClaimBatchAggregate</c>（自身のXML docに「検証や実効版選択を行わない」と明記された raw 型）を
/// 外部から包み直して「検証済み」に見せることはできない。
/// </para>
/// <para>
/// 「実効」の定義: 履歴中の最大 Revision（＝<c>ClaimBatchPolicy.Head</c> と同じ規則。Cancel を
/// 除外してから最大を採ると取消済み請求を過去 revision から復活させてしまう）。head が Cancel、
/// または detail が 0 件なら実効請求は存在しない（provider が null を返す）。
/// </para>
/// </remarks>
public sealed class VerifiedClaimBatch
{
    private VerifiedClaimBatch(ClaimBatch header, IReadOnlyList<ClaimDetail> details)
    {
        Header = header;
        Details = details;
    }

    public ClaimBatch Header { get; }
    public IReadOnlyList<ClaimDetail> Details { get; }

    internal static VerifiedClaimBatch CreateVerified(ClaimBatch header, IReadOnlyList<ClaimDetail> details)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(details);
        return new VerifiedClaimBatch(header, [.. details]);
    }
}
