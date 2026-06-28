using Tsumugi.Domain.Enums;

namespace Tsumugi.Application.Dtos;

public sealed record WageStatementDto(
    Guid Id,
    Guid OfficeId,
    int Year,
    int Month,
    Guid RecipientId,
    int AmountYen,
    string BasisSummary,
    RecordKind Kind,
    Guid? OriginId);
