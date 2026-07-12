using Microsoft.EntityFrameworkCore;
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Infrastructure.Persistence;

public sealed class ClaimInputRepository(TsumugiDbContext db) : IClaimInputRepository
{
    public async Task AddAsync(ClaimInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        await db.ClaimInputs.AddAsync(input, ct);
    }

    public async Task<IReadOnlyList<ClaimInput>> ListHistoryAsync(
        Guid officeId,
        Guid recipientId,
        ServiceMonth serviceMonth,
        CancellationToken ct) =>
        await db.ClaimInputs
            .AsNoTracking()
            .Where(input => input.OfficeId == officeId
                            && input.RecipientId == recipientId
                            && input.ServiceMonth == serviceMonth)
            .OrderBy(input => input.RootId)
            .ThenBy(input => input.Revision)
            .ToArrayAsync(ct);
}
