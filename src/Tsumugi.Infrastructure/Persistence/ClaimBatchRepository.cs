using Microsoft.EntityFrameworkCore;
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Infrastructure.Persistence;

/// <summary>請求の未検証raw aggregateを読み取る。実効版選択はPhase 3-1のvalidated readerの責務。</summary>
public sealed class ClaimBatchRepository(TsumugiDbContext db) : IClaimBatchRepository
{
    public async Task<IReadOnlyList<ClaimBatchAggregate>> ListHistoryAggregatesAsync(
        Guid officeId,
        ServiceMonth serviceMonth,
        CancellationToken ct)
    {
        var headers = await db.ClaimBatches
            .AsNoTracking()
            .Where(batch => batch.OfficeId == officeId && batch.ServiceMonth == serviceMonth)
            .OrderBy(batch => batch.Revision)
            .ToArrayAsync(ct);
        if (headers.Length == 0) return [];

        var headerIds = headers.Select(batch => batch.Id).ToArray();
        var details = await db.ClaimDetails
            .AsNoTracking()
            .Where(detail => headerIds.Contains(detail.ClaimBatchId))
            .OrderBy(detail => detail.RecipientId)
            .ToArrayAsync(ct);
        var byBatch = details.ToLookup(detail => detail.ClaimBatchId);
        return headers
            .Select(header => new ClaimBatchAggregate(header, byBatch[header.Id]))
            .ToArray();
    }

    public async Task<ClaimBatchAggregate?> FindByOperationIdAsync(
        Guid finalizationOperationId,
        CancellationToken ct)
    {
        var header = await db.ClaimBatches
            .AsNoTracking()
            .SingleOrDefaultAsync(
                batch => batch.FinalizationOperationId == finalizationOperationId,
                ct);
        if (header is null) return null;

        var details = await db.ClaimDetails
            .AsNoTracking()
            .Where(detail => detail.ClaimBatchId == header.Id)
            .OrderBy(detail => detail.RecipientId)
            .ToArrayAsync(ct);
        return new ClaimBatchAggregate(header, details);
    }
}
