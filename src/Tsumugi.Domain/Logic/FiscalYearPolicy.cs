using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Logic;

/// <summary>会計年度の導出（純粋関数）。既定の年度起点月は ADR 0012 により 4 月。</summary>
public static class FiscalYearPolicy
{
    public static int Year(DateOnly date, int startMonth)
    {
        if (startMonth is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(startMonth), startMonth, "年度起点月は1〜12の範囲で指定してください。");
        return date.Month >= startMonth ? date.Year : date.Year - 1;
    }

    public static YearMonth FiscalYearStart(int fiscalYear, int startMonth)
    {
        if (startMonth is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(startMonth), startMonth, "年度起点月は1〜12の範囲で指定してください。");
        return new YearMonth(fiscalYear, startMonth);
    }

    public static YearMonth FiscalYearEnd(int fiscalYear, int startMonth)
    {
        if (startMonth is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(startMonth), startMonth, "年度起点月は1〜12の範囲で指定してください。");
        var endMonth = startMonth == 1 ? 12 : startMonth - 1;
        var endYear = startMonth == 1 ? fiscalYear : fiscalYear + 1;
        return new YearMonth(endYear, endMonth);
    }
}
