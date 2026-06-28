using Tsumugi.Domain.Enums;

namespace Tsumugi.Application.Dtos;

public sealed record WorkRecordDto(
    Guid Id,
    Guid RecipientId,
    DateOnly WorkDate,
    RecordKind Kind,
    Guid? OriginId,
    int? WorkedMinutes,
    int? PieceCount,
    int? PieceUnitYen,
    int? Points,
    string? Note);
