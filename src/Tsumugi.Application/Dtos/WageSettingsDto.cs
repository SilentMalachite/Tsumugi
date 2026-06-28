using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Dtos;

public sealed record WageSettingsDto(
    Guid Id,
    Guid OfficeId,
    DateRange Period,
    WageMethod Method,
    RoundingRule Rounding,
    RemainderPolicy Remainder,
    int FiscalYearStartMonth,
    int? FixedDailyYen);
