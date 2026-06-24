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

    public Task<Office?> FindByNumberAsync(string officeNumber, CancellationToken ct) =>
        db.Offices.AsNoTracking().FirstOrDefaultAsync(o => o.OfficeNumber == officeNumber, ct);
}
