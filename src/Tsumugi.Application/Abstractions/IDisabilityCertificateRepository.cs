using Tsumugi.Domain.Entities;

namespace Tsumugi.Application.Abstractions;

public interface IDisabilityCertificateRepository
{
    Task AddAsync(DisabilityCertificate certificate, CancellationToken ct);
    Task<IReadOnlyList<DisabilityCertificate>> ListByRecipientAsync(
        Guid recipientId, CancellationToken ct);
}
