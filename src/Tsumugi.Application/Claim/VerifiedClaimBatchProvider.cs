using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Claim;

/// <summary>
/// 確定請求の「検証済み実効 revision」を解決する唯一の入口。<see cref="IClaimBatchRepository"/> が返す
/// 未検証 raw aggregate を <see cref="ClaimHistoryVerifier"/> に通し、
/// <see cref="VerifiedClaimBatch"/> だけを consumer へ渡す。
/// </summary>
/// <remarks>
/// 請求CSV（<c>ExportClaimCsvUseCase</c>）と3帳票（<c>GenerateClaimReportsUseCase</c>）は
/// どちらもここを経由する。両者が別々に head を選ぶと、同じ確定請求から食い違う成果物が出る。
/// </remarks>
public sealed class VerifiedClaimBatchProvider(
    IClaimBatchRepository batchRepository,
    ClaimHistoryVerifier verifier)
{
    /// <summary>
    /// 実効 revision を返す。履歴が空、head が Cancel（取消済み）、detail が 0 件のいずれかなら
    /// <see langword="null"/>。履歴が検証を通らない場合は <see cref="ClaimFinalizationException"/>。
    /// </summary>
    public async Task<VerifiedClaimBatch?> FindEffectiveAsync(
        Guid officeId,
        ServiceMonth serviceMonth,
        CancellationToken ct)
    {
        var history = await batchRepository.ListHistoryAggregatesAsync(officeId, serviceMonth, ct);
        if (history.Count == 0) return null;

        verifier.Verify(history);

        var head = history.MaxBy(aggregate => aggregate.Header.Revision)!;
        if (head.Header.Kind == RecordKind.Cancel || head.Details.Count == 0) return null;

        return VerifiedClaimBatch.CreateVerified(head.Header, head.Details);
    }
}
