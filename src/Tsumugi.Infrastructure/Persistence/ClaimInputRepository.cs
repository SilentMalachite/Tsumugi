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
            // 汎用 pass-through 入力（ADR 0042）は親 revision の一部なので必ず一緒に読む。
            .Include(input => input.GenericValues)
            .Where(input => input.OfficeId == officeId
                            && input.RecipientId == recipientId
                            && input.ServiceMonth == serviceMonth)
            .OrderBy(input => input.RootId)
            .ThenBy(input => input.Revision)
            .ToArrayAsync(ct);
}
