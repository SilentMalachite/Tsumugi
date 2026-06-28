using Tsumugi.Domain.Enums;

namespace Tsumugi.Application.Dtos;

public sealed record WageFundDto(
    Guid Id,
    Guid OfficeId,
    int Year,
    int Month,
    int TotalYen,
    RecordKind Kind,
    Guid? OriginId,
    string? Note);
