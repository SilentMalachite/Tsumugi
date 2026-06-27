using Tsumugi.Domain.Entities;

namespace Tsumugi.Application.Abstractions;

public interface IContractedProviderRepository
{
    Task AddAsync(ContractedProvider provider, CancellationToken ct);
    Task<ContractedProvider?> FindByIdAsync(Guid id, CancellationToken ct);
    Task UpdateAsync(ContractedProvider provider, CancellationToken ct);
    Task<IReadOnlyList<ContractedProvider>> ListByCertificateAsync(
        Guid certificateId, CancellationToken ct);
}
