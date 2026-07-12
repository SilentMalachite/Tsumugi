using Microsoft.EntityFrameworkCore;
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence;

public sealed class DailyRecordRepository(TsumugiDbContext db) : IDailyRecordRepository
{
    public async Task AddAsync(DailyRecord record, CancellationToken ct) =>
        await db.DailyRecords.AddAsync(record, ct);

    public Task<DailyRecord?> FindByIdAsync(Guid id, CancellationToken ct) =>
        db.DailyRecords.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<DailyRecord>> ListByRecipientAndDateAsync(
        Guid recipientId, DateOnly serviceDate, CancellationToken ct) =>
        (await db.DailyRecords.AsNoTracking()
            .Where(r => r.RecipientId == recipientId && r.ServiceDate == serviceDate)
            .ToListAsync(ct))
            .OrderBy(r => r.CreatedAt)
            .ToArray();

    public async Task<IReadOnlyList<DailyRecord>> ListByRecipientAndMonthAsync(
        Guid recipientId, int year, int month, CancellationToken ct)
    {
        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);
        var records = await db.DailyRecords.AsNoTracking()
            .Where(r => r.RecipientId == recipientId && r.ServiceDate >= from && r.ServiceDate <= to)
            .ToListAsync(ct);
        return records.OrderBy(r => r.ServiceDate).ThenBy(r => r.CreatedAt).ToArray();
    }
}
