using Microsoft.EntityFrameworkCore;
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Infrastructure.Persistence;

public sealed class WageFundRepository(TsumugiDbContext db) : IWageFundRepository
{
    public async Task AddAsync(WageFund fund, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(fund);
        await db.WageFunds.AddAsync(fund, ct);
    }

    public async Task<IReadOnlyList<WageFund>> ListByOfficeAndMonthAsync(
        Guid officeId, int year, int month, CancellationToken ct)
    {
        var ym = new YearMonth(year, month);
        // SQLite は ORDER BY 句で DateTimeOffset を扱えないため、OrderBy(CreatedAt) はクライアント側で実行
        var rows = await db.WageFunds.AsNoTracking()
            .Where(f => f.OfficeId == officeId && f.Month == ym)
            .ToListAsync(ct);
        return rows.OrderBy(f => f.CreatedAt).ToArray();
    }
}
