using Microsoft.EntityFrameworkCore;
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence;

public sealed class RecipientHourlyRateRepository(TsumugiDbContext db) : IRecipientHourlyRateRepository
{
    public async Task AddAsync(RecipientHourlyRate rate, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(rate);
        await db.RecipientHourlyRates.AddAsync(rate, ct);
        // shadow プロパティ PeriodStart を partial unique index のためにセット
        db.Entry(rate).Property("PeriodStart").CurrentValue = rate.Period.Start;
    }

    public async Task<IReadOnlyList<RecipientHourlyRate>> ListByOfficeRecipientAsync(
        Guid officeId, Guid recipientId, CancellationToken ct)
    {
        var rows = await db.RecipientHourlyRates.AsNoTracking()
            .Where(r => r.OfficeId == officeId && r.RecipientId == recipientId)
            .ToListAsync(ct);
        return rows.OrderBy(r => r.CreatedAt).ToArray();
    }
}
