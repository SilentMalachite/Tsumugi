using Microsoft.EntityFrameworkCore;
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence;

public sealed class RecipientRepository(TsumugiDbContext db) : IRecipientRepository
{
    public async Task AddAsync(Recipient recipient, CancellationToken ct) =>
        await db.Recipients.AddAsync(recipient, ct);

    public Task<Recipient?> FindByIdAsync(Guid id, CancellationToken ct) =>
        db.Recipients.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task UpdateAsync(Recipient recipient, CancellationToken ct)
    {
        db.Recipients.Update(recipient);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<Recipient>> ListAsync(bool includeArchived, CancellationToken ct)
    {
        var query = db.Recipients.AsNoTracking().AsQueryable();
        if (!includeArchived)
        {
            query = query.Where(r => r.ArchivedAt == null);
        }
        return await query.OrderBy(r => r.KanaName).ToListAsync(ct);
    }
}
