namespace Tsumugi.Domain.ValueObjects;

/// <summary>国保連CSVを処理する年月。サービス提供年月とは独立して指定する。</summary>
public readonly record struct ProcessingMonth : IComparable<ProcessingMonth>
{
    public int Year { get; }
    public int Month { get; }

    public ProcessingMonth(int year, int month)
    {
        if (year is < 1900 or > 2200)
            throw new ArgumentOutOfRangeException(nameof(year), year, "年は1900〜2200の範囲で指定してください。");
        if (month is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(month), month, "月は1〜12の範囲で指定してください。");

        Year = year;
        Month = month;
    }

    public int ToInt() => Year * 100 + Month;

    public static ProcessingMonth FromInt(int value) => new(value / 100, value % 100);

    public int CompareTo(ProcessingMonth other) => ToInt().CompareTo(other.ToInt());

    public static bool operator <(ProcessingMonth left, ProcessingMonth right) => left.CompareTo(right) < 0;
    public static bool operator <=(ProcessingMonth left, ProcessingMonth right) => left.CompareTo(right) <= 0;
    public static bool operator >(ProcessingMonth left, ProcessingMonth right) => left.CompareTo(right) > 0;
    public static bool operator >=(ProcessingMonth left, ProcessingMonth right) => left.CompareTo(right) >= 0;

    public override string ToString() => $"{Year:D4}-{Month:D2}";
}
