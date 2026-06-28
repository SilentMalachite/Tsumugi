using Tsumugi.Domain.Enums;

namespace Tsumugi.Application.Audit;

/// <summary>
/// 同一性マスタ更新の監査追記を担う薄い抽象。
/// 実装は AuditEntry を append-only リポジトリへ追加するだけで、保存は呼び出し側 UseCase の IUnitOfWork.SaveChangesAsync に委ねる。
/// </summary>
public interface IAuditTrail
{
    Task RecordAsync(
        string actor,
        AuditAction action,
        string targetType,
        Guid targetId,
        DateTimeOffset occurredAt,
        string? summary,
        CancellationToken ct);
}
