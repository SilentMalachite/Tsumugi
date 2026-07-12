using Microsoft.EntityFrameworkCore;
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence;

public sealed class OfficeClaimProfileRepository(TsumugiDbContext db) : IOfficeClaimProfileRepository
{
    public async Task AddAsync(OfficeClaimProfile profile, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(profile);
        await db.OfficeClaimProfiles.AddAsync(profile, ct);
    }

    public async Task<IReadOnlyList<OfficeClaimProfile>> ListByOfficeAsync(
        Guid officeId,
        CancellationToken ct)
    {
        var rows = await db.OfficeClaimProfiles
            .AsNoTracking()
            .Where(profile => profile.OfficeId == officeId)
            .ToArrayAsync(ct);
        return rows
            .OrderBy(profile => profile.EffectiveFrom)
            .ThenBy(profile => profile.EffectiveTo is null)
            .ThenBy(profile => profile.EffectiveTo)
            .ThenBy(profile => profile.RootId)
            .ThenBy(profile => profile.Revision)
            .ToArray();
    }
}
