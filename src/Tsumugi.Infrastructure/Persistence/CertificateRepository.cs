using Microsoft.EntityFrameworkCore;
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence;

public sealed class CertificateRepository(TsumugiDbContext db) : ICertificateRepository
{
    public async Task AddAsync(Certificate certificate, CancellationToken ct) =>
        await db.Certificates.AddAsync(certificate, ct);

    public async Task<IReadOnlyList<Certificate>> ListByRecipientAsync(Guid recipientId, CancellationToken ct) =>
        await db.Certificates.AsNoTracking()
            .Where(c => c.RecipientId == recipientId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Certificate>> ListAllAsync(CancellationToken ct) =>
        await db.Certificates.AsNoTracking().ToListAsync(ct);

    public async Task<Certificate?> FindEffectiveAsync(Guid recipientId, DateOnly asOf, CancellationToken ct)
    {
        // DateRange は JSON 列のため SQL レベルでフィルタできない。
        // インデックスのある RecipientId で絞り込んだあとメモリで実効判定。
        var candidates = await db.Certificates.AsNoTracking()
            .Where(c => c.RecipientId == recipientId)
            .ToListAsync(ct);
        // 同期日内に複数あれば「最新の CreatedAt」が実効
        return candidates
            .Where(c => c.Validity.Contains(asOf))
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefault();
    }
}
