using Tsumugi.Domain.Entities;

namespace Tsumugi.Application.Abstractions;

public interface IRecipientRepository
{
    Task AddAsync(Recipient recipient, CancellationToken ct);
    Task<Recipient?> FindByIdAsync(Guid id, CancellationToken ct);
    Task UpdateAsync(Recipient recipient, CancellationToken ct);
    Task<IReadOnlyList<Recipient>> ListAsync(CancellationToken ct);
}
