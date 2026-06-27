using Tsumugi.Domain.Entities;

namespace Tsumugi.Application.Abstractions;

public interface IDailyRecordRepository
{
    Task AddAsync(DailyRecord record, CancellationToken ct);
    Task<DailyRecord?> FindByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<DailyRecord>> ListByRecipientAndDateAsync(
        Guid recipientId, DateOnly serviceDate, CancellationToken ct);
    Task<IReadOnlyList<DailyRecord>> ListByRecipientAndMonthAsync(
        Guid recipientId, int year, int month, CancellationToken ct);
}
