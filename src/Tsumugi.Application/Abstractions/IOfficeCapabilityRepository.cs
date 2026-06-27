using Tsumugi.Domain.Entities;

namespace Tsumugi.Application.Abstractions;

public interface IOfficeCapabilityRepository
{
    Task AddAsync(OfficeCapability capability, CancellationToken ct);
    Task<IReadOnlyList<OfficeCapability>> ListByOfficeAsync(Guid officeId, CancellationToken ct);
    Task<OfficeCapability?> FindEffectiveAsync(Guid officeId, DateOnly asOf, CancellationToken ct);
}
