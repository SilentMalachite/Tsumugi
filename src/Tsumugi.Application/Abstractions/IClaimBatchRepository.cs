using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Abstractions;

public interface IClaimBatchRepository
{
    Task<IReadOnlyList<ClaimBatchAggregate>> ListHistoryAggregatesAsync(
        Guid officeId, ServiceMonth serviceMonth, CancellationToken ct);

    Task<ClaimBatchAggregate?> FindByOperationIdAsync(
        Guid finalizationOperationId, CancellationToken ct);
}
