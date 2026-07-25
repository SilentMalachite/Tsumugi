using Tsumugi.Domain.Entities;

namespace Tsumugi.Application.Abstractions;

/// <summary>
/// 国保連CSV出力履歴の追記専用リポジトリ。更新・削除の口を持たない（ADR 0014）。
/// </summary>
public interface IClaimCsvExportRepository
{
    Task AppendAsync(ClaimCsvExport csvExport, CancellationToken ct);

    /// <summary>指定した請求バッチの出力履歴を作成日時の昇順で返す。</summary>
    Task<IReadOnlyList<ClaimCsvExport>> ListByBatchAsync(Guid claimBatchId, CancellationToken ct);
}
