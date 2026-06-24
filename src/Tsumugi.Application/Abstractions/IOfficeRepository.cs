using Tsumugi.Domain.Entities;

namespace Tsumugi.Application.Abstractions;

public interface IOfficeRepository
{
    Task AddAsync(Office office, CancellationToken ct);
    Task<Office?> FindByNumberAsync(string officeNumber, CancellationToken ct);
}
