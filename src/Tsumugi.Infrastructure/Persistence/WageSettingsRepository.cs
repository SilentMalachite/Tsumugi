using Microsoft.EntityFrameworkCore;
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence;

public sealed class WageSettingsRepository(TsumugiDbContext db) : IWageSettingsRepository
{
    public async Task AddAsync(WageSettings settings, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(settings);
        await db.WageSettings.AddAsync(settings, ct);
    }

    public async Task<IReadOnlyList<WageSettings>> ListByOfficeAsync(
        Guid officeId, CancellationToken ct)
    {
        // SQLite は ORDER BY 句で DateTimeOffset を扱えないため、OrderBy(CreatedAt) はクライアント側で実行
        var rows = await db.WageSettings.AsNoTracking()
            .Where(s => s.OfficeId == officeId)
            .ToListAsync(ct);
        return rows.OrderByDescending(s => s.CreatedAt).ToArray();
    }
}
