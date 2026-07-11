using Tsumugi.Domain.Entities;

namespace Tsumugi.Application.Audit;

public interface IClaimAuditEntryFactory
{
    AuditEntry Create(
        Guid auditEntryId,
        string actor,
        ClaimAuditPayload payload,
        DateTimeOffset occurredAt);
}
