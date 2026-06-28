using Tsumugi.Domain.Entities;

namespace Tsumugi.Application.Abstractions;

public interface IAuditEntryRepository
{
    Task AddAsync(AuditEntry entry, CancellationToken ct);
    Task<IReadOnlyList<AuditEntry>> ListByTargetAsync(
        string targetType, Guid targetId, CancellationToken ct);
}
