using Tsumugi.Domain.Entities;

namespace Tsumugi.Application.Abstractions;

public interface ICertificateRepository
{
    Task AddAsync(Certificate certificate, CancellationToken ct);
    async Task<Certificate?> FindHeadByRootIdAsync(Guid rootCertificateId, CancellationToken ct)
    {
        var all = await ListAllAsync(ct);
        return all
            .Where(certificate => certificate.RootCertificateId == rootCertificateId)
            .MaxBy(certificate => certificate.Revision);
    }
    Task<IReadOnlyList<Certificate>> ListByRecipientAsync(Guid recipientId, CancellationToken ct);
    Task<IReadOnlyList<Certificate>> ListAllAsync(CancellationToken ct);
    Task<Certificate?> FindEffectiveAsync(Guid recipientId, DateOnly asOf, CancellationToken ct);
}
