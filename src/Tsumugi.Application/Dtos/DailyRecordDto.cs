using Tsumugi.Domain.Enums;

namespace Tsumugi.Application.Dtos;

public sealed record DailyRecordDto(
    Guid Id, Guid RecipientId, DateOnly ServiceDate,
    RecordKind Kind, Guid? OriginId,
    Attendance Attendance, TransportKind Transport, bool MealProvided, string? Note);
