using Tsumugi.Domain.Entities;

namespace Tsumugi.Application.Abstractions;

public interface IWorkRecordRepository
{
    Task AddAsync(WorkRecord record, CancellationToken ct);
    Task<WorkRecord?> FindByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<WorkRecord>> ListByRecipientAndMonthAsync(
        Guid recipientId, int year, int month, CancellationToken ct);
}
