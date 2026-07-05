using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Abstractions;

public interface IWageAdjustmentRepository
{
    Task AddAsync(WageAdjustment adjustment, CancellationToken ct);
    Task<IReadOnlyList<WageAdjustment>> ListByOfficeMonthAsync(
        Guid officeId, YearMonth yearMonth, CancellationToken ct);
}
