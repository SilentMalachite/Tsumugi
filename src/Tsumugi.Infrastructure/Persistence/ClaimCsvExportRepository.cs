using Microsoft.EntityFrameworkCore;
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence;

/// <summary>国保連CSV出力履歴の追記専用リポジトリ実装。</summary>
public sealed class ClaimCsvExportRepository(TsumugiDbContext db) : IClaimCsvExportRepository
{
    public async Task AppendAsync(ClaimCsvExport csvExport, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(csvExport);
        db.ClaimCsvExports.Add(csvExport);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ClaimCsvExport>> ListByBatchAsync(
        Guid claimBatchId,
        CancellationToken ct)
    {
        // SQLite は ORDER BY 句で DateTimeOffset を扱えないため、OrderBy(CreatedAt) はクライアント側で実行
        var rows = await db.ClaimCsvExports
            .AsNoTracking()
            .Where(csvExport => csvExport.ClaimBatchId == claimBatchId)
            .ToListAsync(ct);
        return rows
            .OrderBy(csvExport => csvExport.CreatedAt)
            .ThenBy(csvExport => csvExport.Id)
            .ToArray();
    }
}
