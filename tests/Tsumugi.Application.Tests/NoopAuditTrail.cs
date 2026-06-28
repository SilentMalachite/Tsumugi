using Tsumugi.Application.Audit;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Application.Tests;

/// <summary>監査記録を捨てる no-op 実装。AuditEntry 配線そのものを検証しないテストで使う。</summary>
internal sealed class NoopAuditTrail : IAuditTrail
{
    public Task RecordAsync(
        string actor, AuditAction action, string targetType, Guid targetId,
        DateTimeOffset occurredAt, string? summary, CancellationToken ct)
        => Task.CompletedTask;
}

/// <summary>呼び出しを記録する fake。配線テストで使う。</summary>
internal sealed class RecordingAuditTrail : IAuditTrail
{
    public List<AuditCall> Calls { get; } = new();

    public Task RecordAsync(
        string actor, AuditAction action, string targetType, Guid targetId,
        DateTimeOffset occurredAt, string? summary, CancellationToken ct)
    {
        Calls.Add(new AuditCall(actor, action, targetType, targetId, occurredAt, summary));
        return Task.CompletedTask;
    }
}

internal sealed record AuditCall(
    string Actor, AuditAction Action, string TargetType, Guid TargetId,
    DateTimeOffset OccurredAt, string? Summary);
