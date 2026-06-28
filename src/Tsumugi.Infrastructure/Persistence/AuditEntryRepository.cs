using Microsoft.EntityFrameworkCore;
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence;

public sealed class AuditEntryRepository(TsumugiDbContext db) : IAuditEntryRepository
{
    public async Task AddAsync(AuditEntry entry, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entry);
        await db.AuditEntries.AddAsync(entry, ct);
    }

    public async Task<IReadOnlyList<AuditEntry>> ListByTargetAsync(
        string targetType, Guid targetId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(targetType);
        // SQLite は ORDER BY 句で DateTimeOffset を扱えないため、OrderBy(OccurredAt) はクライアント側で実行
        var rows = await db.AuditEntries.AsNoTracking()
            .Where(e => e.TargetType == targetType && e.TargetId == targetId)
            .ToListAsync(ct);
        return rows.OrderBy(e => e.OccurredAt).ToArray();
    }
}
