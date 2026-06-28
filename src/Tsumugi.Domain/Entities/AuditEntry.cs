using Tsumugi.Domain.Enums;

namespace Tsumugi.Domain.Entities;

/// <summary>同一性マスタ更新時の監査追記。誰が・いつ・何を・概要を残す。</summary>
public sealed record AuditEntry : Entity
{
    public required string Actor { get; init; }
    public required AuditAction Action { get; init; }
    public required string TargetType { get; init; }
    public required Guid TargetId { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
    public string? Summary { get; init; }

    public static AuditEntry Create(
        Guid id, string actor, AuditAction action,
        string targetType, Guid targetId,
        DateTimeOffset occurredAt, string? summary,
        DateTimeOffset createdAt, string createdBy)
    {
        ArgumentException.ThrowIfNullOrEmpty(actor);
        ArgumentException.ThrowIfNullOrEmpty(targetType);
        return new AuditEntry
        {
            Id = id,
            Actor = actor,
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            OccurredAt = occurredAt,
            Summary = summary,
            CreatedAt = createdAt,
            CreatedBy = createdBy,
            ConcurrencyToken = Guid.Empty,
        };
    }
}
