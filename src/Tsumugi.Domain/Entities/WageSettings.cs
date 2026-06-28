using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Entities;

/// <summary>工賃設定（期間マスタ、追記）。基準日時点の方式・端数規則・年度起点を引く。</summary>
public sealed record WageSettings : Entity
{
    public required Guid OfficeId { get; init; }
    public required DateRange Period { get; init; }
    public required WageMethod Method { get; init; }
    public required RoundingRule Rounding { get; init; }
    public required RemainderPolicy Remainder { get; init; }
    public required int FiscalYearStartMonth { get; init; }
    public int? FixedDailyYen { get; init; }

    public static WageSettings Create(
        Guid id, Guid officeId, DateRange period,
        WageMethod method, RoundingRule rounding, RemainderPolicy remainder,
        int fiscalYearStartMonth, int? fixedDailyYen,
        string createdBy, DateTimeOffset createdAt)
    {
        if (fiscalYearStartMonth is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(fiscalYearStartMonth),
                fiscalYearStartMonth, "年度起点月は1〜12の範囲で指定してください。");
        if (method == WageMethod.Fixed && fixedDailyYen is null)
            throw new ArgumentException("Fixed 方式では FixedDailyYen を指定してください。", nameof(fixedDailyYen));
        if (fixedDailyYen is { } y && y < 0)
            throw new ArgumentOutOfRangeException(nameof(fixedDailyYen), y, "FixedDailyYen は0円以上で指定してください。");
        return new WageSettings
        {
            Id = id,
            OfficeId = officeId,
            Period = period,
            Method = method,
            Rounding = rounding,
            Remainder = remainder,
            FiscalYearStartMonth = fiscalYearStartMonth,
            FixedDailyYen = fixedDailyYen,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ConcurrencyToken = Guid.NewGuid(),
        };
    }
}
