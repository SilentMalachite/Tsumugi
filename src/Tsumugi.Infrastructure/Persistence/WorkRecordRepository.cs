using Microsoft.EntityFrameworkCore;
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence;

public sealed class WorkRecordRepository(TsumugiDbContext db) : IWorkRecordRepository
{
    public async Task AddAsync(WorkRecord record, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(record);
        await db.WorkRecords.AddAsync(record, ct);
    }

    public Task<WorkRecord?> FindByIdAsync(Guid id, CancellationToken ct) =>
        db.WorkRecords.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<WorkRecord>> ListByRecipientAndMonthAsync(
        Guid recipientId, int year, int month, CancellationToken ct)
    {
        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);
        // SQLite は ORDER BY 句で DateTimeOffset を扱えないため、ThenBy(CreatedAt) はクライアント側で実行
        var rows = await db.WorkRecords.AsNoTracking()
            .Where(r => r.RecipientId == recipientId && r.WorkDate >= from && r.WorkDate <= to)
            .OrderBy(r => r.WorkDate)
            .ToListAsync(ct);
        return rows.OrderBy(r => r.WorkDate).ThenBy(r => r.CreatedAt).ToArray();
    }
}
