using Microsoft.EntityFrameworkCore;
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Entities;
using ClaimCertificatePolicy = Tsumugi.Domain.Logic.Claim.CertificatePolicy;

namespace Tsumugi.Infrastructure.Persistence;

public sealed class CertificateRepository(TsumugiDbContext db) : ICertificateRepository
{
    public async Task AddAsync(Certificate certificate, CancellationToken ct) =>
        await db.Certificates.AddAsync(certificate, ct);

    public async Task<Certificate?> FindHeadByRootIdAsync(
        Guid rootCertificateId,
        CancellationToken ct) =>
        await db.Certificates.AsNoTracking()
            .Where(certificate => certificate.RootCertificateId == rootCertificateId)
            .OrderByDescending(certificate => certificate.Revision)
            .FirstOrDefaultAsync(ct);

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
        return ClaimCertificatePolicy.EffectiveVersion(candidates, asOf);
    }
}
