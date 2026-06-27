using Microsoft.EntityFrameworkCore;
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence;

public sealed class ContractRepository(TsumugiDbContext db) : IContractRepository
{
    public async Task AddAsync(Contract contract, CancellationToken ct) =>
        await db.Contracts.AddAsync(contract, ct);

    public async Task<IReadOnlyList<Contract>> ListByRecipientAsync(Guid recipientId, CancellationToken ct) =>
        await db.Contracts.AsNoTracking()
            .Where(c => c.RecipientId == recipientId)
            .ToListAsync(ct);

    public async Task<Contract?> FindEffectiveAsync(Guid recipientId, DateOnly asOf, CancellationToken ct)
    {
        // DateRange は JSON 列のため SQL レベルでフィルタできない。
        // インデックスのある RecipientId で絞り込んだあとメモリで実効判定。
        var candidates = await db.Contracts.AsNoTracking()
            .Where(c => c.RecipientId == recipientId)
            .ToListAsync(ct);
        return candidates
            .Where(c => c.Period.Contains(asOf))
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefault();
    }
}
