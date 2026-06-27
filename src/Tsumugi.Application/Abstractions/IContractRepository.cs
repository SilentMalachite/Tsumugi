using Tsumugi.Domain.Entities;

namespace Tsumugi.Application.Abstractions;

public interface IContractRepository
{
    Task AddAsync(Contract contract, CancellationToken ct);
    Task<IReadOnlyList<Contract>> ListByRecipientAsync(Guid recipientId, CancellationToken ct);
    Task<Contract?> FindEffectiveAsync(Guid recipientId, DateOnly asOf, CancellationToken ct);
}
