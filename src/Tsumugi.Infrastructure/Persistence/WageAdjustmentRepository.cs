using Microsoft.EntityFrameworkCore;
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Infrastructure.Persistence;

public sealed class WageAdjustmentRepository(TsumugiDbContext db) : IWageAdjustmentRepository
{
    public async Task AddAsync(WageAdjustment adjustment, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(adjustment);
        await db.WageAdjustments.AddAsync(adjustment, ct);
    }

    public async Task<IReadOnlyList<WageAdjustment>> ListByOfficeMonthAsync(
        Guid officeId, YearMonth yearMonth, CancellationToken ct)
    {
        // SQLite は ORDER BY 句で DateTimeOffset を扱えないため、OrderBy(CreatedAt) はクライアント側で実行
        var rows = await db.WageAdjustments.AsNoTracking()
            .Where(w => w.OfficeId == officeId && w.YearMonth == yearMonth)
            .ToListAsync(ct);
        return rows.OrderBy(w => w.CreatedAt).ToArray();
    }
}
