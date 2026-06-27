using Tsumugi.Domain.Entities;

namespace Tsumugi.Application.Abstractions;

public interface IOfficeRepository
{
    Task AddAsync(Office office, CancellationToken ct);
    Task<Office?> FindByIdAsync(Guid id, CancellationToken ct);
    Task<Office?> FindByNumberAsync(string officeNumber, CancellationToken ct);
    Task UpdateAsync(Office office, CancellationToken ct);
    Task<IReadOnlyList<Office>> ListAsync(CancellationToken ct);
}
