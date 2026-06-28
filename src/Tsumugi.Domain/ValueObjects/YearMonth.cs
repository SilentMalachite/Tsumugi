namespace Tsumugi.Domain.ValueObjects;

/// <summary>年月。両端含む暦日範囲 [FirstDay, LastDay] に対応する不可変値オブジェクト。</summary>
public readonly record struct YearMonth : IComparable<YearMonth>
{
    public int Year { get; }
    public int Month { get; }

    public YearMonth(int year, int month)
    {
        if (year is < 1900 or > 2200)
            throw new ArgumentOutOfRangeException(nameof(year), year, "年は1900〜2200の範囲で指定してください。");
        if (month is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(month), month, "月は1〜12の範囲で指定してください。");
        Year = year;
        Month = month;
    }

    public DateOnly FirstDay() => new(Year, Month, 1);
    public DateOnly LastDay() => new(Year, Month, DateTime.DaysInMonth(Year, Month));

    public YearMonth Next() => Month == 12 ? new YearMonth(Year + 1, 1) : new YearMonth(Year, Month + 1);
    public YearMonth Previous() => Month == 1 ? new YearMonth(Year - 1, 12) : new YearMonth(Year, Month - 1);

    public static YearMonth FromDate(DateOnly date) => new(date.Year, date.Month);

    /// <summary>整数化（YYYYMM 形式）。SQLite 列への永続化と整列に使う。</summary>
    public int ToInt() => Year * 100 + Month;

    /// <summary>YYYYMM 形式の整数から復元。</summary>
    public static YearMonth FromInt(int value) => new(value / 100, value % 100);

    public int CompareTo(YearMonth other)
    {
        var byYear = Year.CompareTo(other.Year);
        return byYear != 0 ? byYear : Month.CompareTo(other.Month);
    }

    public static bool operator <(YearMonth left, YearMonth right) => left.CompareTo(right) < 0;
    public static bool operator <=(YearMonth left, YearMonth right) => left.CompareTo(right) <= 0;
    public static bool operator >(YearMonth left, YearMonth right) => left.CompareTo(right) > 0;
    public static bool operator >=(YearMonth left, YearMonth right) => left.CompareTo(right) >= 0;

    public override string ToString() => $"{Year:D4}-{Month:D2}";
}
