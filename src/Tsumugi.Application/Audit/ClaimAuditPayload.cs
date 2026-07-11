using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Audit;

public sealed record ClaimAuditPayload(
    string EventCode,
    Guid BatchId,
    Guid OperationId,
    Guid OfficeId,
    ServiceMonth ServiceMonth,
    RecordKind Kind,
    int Revision,
    Guid? RootId,
    string OperationHash)
{
    public const string FinalizedEventCode = "claim-finalized";
}
