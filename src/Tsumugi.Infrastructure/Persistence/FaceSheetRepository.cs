using Microsoft.EntityFrameworkCore;
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence;

public sealed class FaceSheetRepository(TsumugiDbContext db) : IFaceSheetRepository
{
    public async Task AddAsync(FaceSheet faceSheet, CancellationToken ct) =>
        await db.FaceSheets.AddAsync(faceSheet, ct);

    public async Task<FaceSheet?> FindLatestByRecipientAsync(Guid recipientId, CancellationToken ct)
    {
        // SQLite は ORDER BY DateTimeOffset を直接サポートしないため、
        // RecipientId で絞り込んだ後にメモリ上で並べ替える。
        // 同一利用者のフェースシート版数は実運用で数十件以下を想定する。
        var rows = await db.FaceSheets.AsNoTracking()
            .Where(f => f.RecipientId == recipientId)
            .ToListAsync(ct);
        return rows.OrderByDescending(f => f.CreatedAt).FirstOrDefault();
    }
}
