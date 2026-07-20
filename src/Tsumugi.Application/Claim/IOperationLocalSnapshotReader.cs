using Tsumugi.Domain.Logic.Claim;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Claim;

/// <summary>
/// 請求確定時点のOffice/Recipient/Certificate/DailyRecord/IntensiveSupportEpisode/ClaimInputを
/// 単一の受給者について集約し、<see cref="ClaimFinalizationSnapshot"/>（spec §6 v2 finalization
/// payload）を構築する。読み取り対象はすべて「確定操作を実行している現在」のローカルDB状態
/// （operation-local）であり、後続の帳票生成（<c>IClaimBatchRepository</c>経由）はこの結果を
/// 経由したsnapshotだけを参照しライブ状態を再読込しない（spec §5.3 単一入力源）。
/// </summary>
public interface IOperationLocalSnapshotReader
{
    Task<ClaimFinalizationSnapshot> ReadAsync(
        Guid officeId,
        Guid recipientId,
        ServiceMonth serviceMonth,
        RecipientClaimResult calculationResult,
        string claimMasterVersion,
        string csvSpecificationVersion,
        string reportSpecificationVersion,
        CancellationToken ct);
}
