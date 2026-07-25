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
        // 汎用 pass-through 入力（ADR 0042）は片方向 FK の子行なので、親のナビゲーション経由の
        // 連鎖挿入に頼らず明示的に追加する（明細行と同じ扱い）。
        if (input.GenericValues.Count > 0)
        {
            await db.ClaimInputGenericValues.AddRangeAsync(input.GenericValues, ct);
        }
    }

    public async Task<IReadOnlyList<ClaimInput>> ListHistoryAsync(
        Guid officeId,
        Guid recipientId,
        ServiceMonth serviceMonth,
        CancellationToken ct)
    {
        var history = await db.ClaimInputs
            .AsNoTracking()
            .Where(input => input.OfficeId == officeId
                            && input.RecipientId == recipientId
                            && input.ServiceMonth == serviceMonth)
            .OrderBy(input => input.RootId)
            .ThenBy(input => input.Revision)
            .ToArrayAsync(ct);
        if (history.Length == 0) return [];

        var inputIds = history.Select(input => input.Id).ToArray();
        var genericValues = await db.ClaimInputGenericValues
            .AsNoTracking()
            .Where(value => inputIds.Contains(value.ClaimInputId))
            .OrderBy(value => value.ClaimInputId)
            .ThenBy(value => value.Name)
            .ToArrayAsync(ct);
        if (genericValues.Length == 0) return history;

        var valuesByInputId = genericValues.ToLookup(value => value.ClaimInputId);
        return [.. history.Select(input => valuesByInputId[input.Id] is { } values && values.Any()
            ? input with { GenericValues = [.. values] }
            : input)];
    }
}
