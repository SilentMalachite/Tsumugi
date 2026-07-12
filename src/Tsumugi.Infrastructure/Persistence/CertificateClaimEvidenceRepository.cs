using Microsoft.EntityFrameworkCore;
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence;

public sealed class CertificateClaimEvidenceRepository(TsumugiDbContext db)
    : ICertificateClaimEvidenceRepository
{
    public async Task AddAsync(CertificateClaimEvidence evidence, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        await db.CertificateClaimEvidences.AddAsync(evidence, ct);
    }

    public async Task<IReadOnlyList<CertificateClaimEvidence>> ListByCertificateAsync(
        Guid certificateId,
        CancellationToken ct)
    {
        var rows = await db.CertificateClaimEvidences
            .AsNoTracking()
            .Where(evidence => evidence.CertificateId == certificateId)
            .ToArrayAsync(ct);
        return rows
            .OrderBy(evidence => evidence.Validity.Start)
            .ThenBy(evidence => evidence.Validity.End is null)
            .ThenBy(evidence => evidence.Validity.End)
            .ThenBy(evidence => evidence.RootId)
            .ThenBy(evidence => evidence.Revision)
            .ToArray();
    }
}
