using Tsumugi.Application.Audit;
using Tsumugi.Domain.Enums;

namespace Tsumugi.App.Tests;

internal sealed class NoopAuditTrail : IAuditTrail
{
    public Task RecordAsync(
        string actor, AuditAction action, string targetType, Guid targetId,
        DateTimeOffset occurredAt, string? summary, CancellationToken ct)
        => Task.CompletedTask;
}
