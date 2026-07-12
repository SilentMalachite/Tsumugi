using Microsoft.EntityFrameworkCore;
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence;

public sealed class IntensiveSupportEpisodeRepository(TsumugiDbContext db)
    : IIntensiveSupportEpisodeRepository
{
    public async Task AddAsync(IntensiveSupportEpisode episode, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(episode);
        await db.IntensiveSupportEpisodes.AddAsync(episode, ct);
    }

    public async Task<IReadOnlyList<IntensiveSupportEpisode>> ListHistoryAsync(
        Guid officeId,
        Guid recipientId,
        CancellationToken ct) =>
        await db.IntensiveSupportEpisodes
            .AsNoTracking()
            .Where(episode => episode.OfficeId == officeId && episode.RecipientId == recipientId)
            .OrderBy(episode => episode.RootId)
            .ThenBy(episode => episode.Revision)
            .ToArrayAsync(ct);
}
