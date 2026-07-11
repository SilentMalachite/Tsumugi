using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Application.Audit;

public sealed class ClaimAuditEntryFactory : IClaimAuditEntryFactory
{
    public AuditEntry Create(
        Guid auditEntryId,
        string actor,
        ClaimAuditPayload payload,
        DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (!string.Equals(
                payload.EventCode,
                ClaimAuditPayload.FinalizedEventCode,
                StringComparison.Ordinal)
            || payload.BatchId == Guid.Empty
            || payload.OperationId == Guid.Empty
            || payload.OfficeId == Guid.Empty
            || payload.Revision < 1
            || payload.OperationHash is not { Length: 64 }
            || payload.OperationHash.Any(character =>
                character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
            throw new ArgumentException("ClaimAuditPayloadが不正です。", nameof(payload));

        var summary = string.Join(';',
            $"eventCode={payload.EventCode}",
            $"batchId={Format(payload.BatchId)}",
            $"operationId={Format(payload.OperationId)}",
            $"officeId={Format(payload.OfficeId)}",
            $"serviceMonth={payload.ServiceMonth}",
            $"kind={(int)payload.Kind}",
            $"revision={payload.Revision}",
            $"rootId={(payload.RootId is null ? "null" : Format(payload.RootId.Value))}",
            $"operationHash={payload.OperationHash}");

        if (summary.Length > 512)
            throw new ClaimFinalizationException(ClaimErrorCode.InvalidOperationPayload);

        return AuditEntry.Create(
            auditEntryId,
            actor,
            AuditAction.Register,
            nameof(ClaimBatch),
            payload.BatchId,
            occurredAt,
            summary,
            occurredAt,
            actor);
    }

    private static string Format(Guid value) => value.ToString("D").ToLowerInvariant();
}
