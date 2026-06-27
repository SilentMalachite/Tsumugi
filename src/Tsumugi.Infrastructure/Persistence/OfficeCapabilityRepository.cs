using Microsoft.EntityFrameworkCore;
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence;

public sealed class OfficeCapabilityRepository(TsumugiDbContext db) : IOfficeCapabilityRepository
{
    public async Task AddAsync(OfficeCapability capability, CancellationToken ct) =>
        await db.OfficeCapabilities.AddAsync(capability, ct);

    public async Task<IReadOnlyList<OfficeCapability>> ListByOfficeAsync(Guid officeId, CancellationToken ct) =>
        await db.OfficeCapabilities.AsNoTracking()
            .Where(c => c.OfficeId == officeId)
            .ToListAsync(ct);

    public async Task<OfficeCapability?> FindEffectiveAsync(Guid officeId, DateOnly asOf, CancellationToken ct)
    {
        // DateRange は JSON 列のため SQL レベルでフィルタできない。
        // インデックスのある OfficeId で絞り込んだあとメモリで実効判定。
        var candidates = await db.OfficeCapabilities.AsNoTracking()
            .Where(c => c.OfficeId == officeId)
            .ToListAsync(ct);
        return candidates
            .Where(c => c.Period.Contains(asOf))
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefault();
    }
}
