using Tsumugi.Domain.Entities;

namespace Tsumugi.Application.Abstractions;

public interface IWageSettingsRepository
{
    Task AddAsync(WageSettings settings, CancellationToken ct);
    Task<IReadOnlyList<WageSettings>> ListByOfficeAsync(Guid officeId, CancellationToken ct);
}
