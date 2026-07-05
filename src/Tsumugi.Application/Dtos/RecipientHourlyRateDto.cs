using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Dtos;

public sealed record RecipientHourlyRateDto(
    Guid Id, Guid OfficeId, Guid RecipientId, DateRange Period,
    int HourlyYen, RecordKind Kind, Guid? OriginId, string? Note);
