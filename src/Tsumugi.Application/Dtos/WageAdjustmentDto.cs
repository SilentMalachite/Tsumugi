using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Dtos;

public sealed record WageAdjustmentDto(
    Guid Id, Guid OfficeId, Guid RecipientId, YearMonth YearMonth,
    WageAdjustmentType Type, int AmountYen,
    RecordKind Kind, Guid? OriginId, string? Note);
