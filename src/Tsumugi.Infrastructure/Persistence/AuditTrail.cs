using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Audit;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Infrastructure.Persistence;

/// <summary>
/// 既定の監査追記。AuditEntry を append-only リポジトリへ追加するのみ。
/// 保存は呼び出し UseCase の IUnitOfWork.SaveChangesAsync に委ねる。
/// </summary>
public sealed class AuditTrail(IAuditEntryRepository repo, TimeProvider clock) : IAuditTrail
{
    public async Task RecordAsync(
        string actor, AuditAction action, string targetType, Guid targetId,
        DateTimeOffset occurredAt, string? summary, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        ArgumentException.ThrowIfNullOrEmpty(targetType);
        var entry = AuditEntry.Create(
            Guid.NewGuid(), actor, action, targetType, targetId,
            occurredAt, summary, clock.GetUtcNow(), actor);
        await repo.AddAsync(entry, ct);
    }
}
