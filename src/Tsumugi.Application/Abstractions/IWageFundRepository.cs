using Tsumugi.Domain.Entities;

namespace Tsumugi.Application.Abstractions;

public interface IWageFundRepository
{
    Task AddAsync(WageFund fund, CancellationToken ct);
    Task<IReadOnlyList<WageFund>> ListByOfficeAndMonthAsync(
        Guid officeId, int year, int month, CancellationToken ct);
}
