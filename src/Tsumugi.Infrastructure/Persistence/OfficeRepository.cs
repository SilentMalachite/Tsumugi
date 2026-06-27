using Microsoft.EntityFrameworkCore;
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence;

public sealed class OfficeRepository(TsumugiDbContext db) : IOfficeRepository
{
    public async Task AddAsync(Office office, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(office);
        await db.Offices.AddAsync(office, ct);
    }

    public Task<Office?> FindByIdAsync(Guid id, CancellationToken ct) =>
        db.Offices.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, ct);

    public Task<Office?> FindByNumberAsync(string officeNumber, CancellationToken ct) =>
        db.Offices.AsNoTracking().FirstOrDefaultAsync(o => o.OfficeNumber == officeNumber, ct);

    public Task UpdateAsync(Office office, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(office);
        db.Offices.Update(office);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<Office>> ListAsync(CancellationToken ct) =>
        await db.Offices.AsNoTracking().OrderBy(o => o.OfficeNumber).ToListAsync(ct);
}
