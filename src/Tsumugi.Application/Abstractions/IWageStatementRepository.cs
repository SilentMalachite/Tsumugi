using Tsumugi.Domain.Entities;

namespace Tsumugi.Application.Abstractions;

public interface IWageStatementRepository
{
    Task AddAsync(WageStatement statement, CancellationToken ct);
    Task<IReadOnlyList<WageStatement>> ListByOfficeAndMonthAsync(
        Guid officeId, int year, int month, CancellationToken ct);
}
