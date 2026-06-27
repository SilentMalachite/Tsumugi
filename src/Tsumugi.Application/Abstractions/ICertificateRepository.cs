using Tsumugi.Domain.Entities;

namespace Tsumugi.Application.Abstractions;

public interface ICertificateRepository
{
    Task AddAsync(Certificate certificate, CancellationToken ct);
    Task<IReadOnlyList<Certificate>> ListByRecipientAsync(Guid recipientId, CancellationToken ct);
    Task<IReadOnlyList<Certificate>> ListAllAsync(CancellationToken ct);
    Task<Certificate?> FindEffectiveAsync(Guid recipientId, DateOnly asOf, CancellationToken ct);
}
