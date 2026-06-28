using Microsoft.EntityFrameworkCore;
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Infrastructure.Persistence;

public sealed class WageStatementRepository(TsumugiDbContext db) : IWageStatementRepository
{
    public async Task AddAsync(WageStatement statement, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(statement);
        await db.WageStatements.AddAsync(statement, ct);
    }

    public async Task<IReadOnlyList<WageStatement>> ListByOfficeAndMonthAsync(
        Guid officeId, int year, int month, CancellationToken ct)
    {
        var ym = new YearMonth(year, month);
        // SQLite は ORDER BY 句で DateTimeOffset を扱えないため、OrderBy(CreatedAt) はクライアント側で実行
        var rows = await db.WageStatements.AsNoTracking()
            .Where(s => s.OfficeId == officeId && s.Month == ym)
            .ToListAsync(ct);
        return rows.OrderBy(s => s.CreatedAt).ToArray();
    }
}
