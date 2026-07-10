namespace Tsumugi.Domain.ValueObjects;

/// <summary>国保連CSVを処理する年月。サービス提供年月とは独立して指定する。</summary>
public readonly record struct ProcessingMonth : IComparable<ProcessingMonth>
{
    private readonly int _year;
    private readonly int _month;

    public int Year
    {
        get
        {
            EnsureValid();
            return _year;
        }
    }

    public int Month
    {
        get
        {
            EnsureValid();
            return _month;
        }
    }

    public ProcessingMonth(int year, int month)
    {
        if (year is < 1900 or > 2200)
            throw new ArgumentOutOfRangeException(nameof(year), year, "年は1900〜2200の範囲で指定してください。");
        if (month is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(month), month, "月は1〜12の範囲で指定してください。");

        _year = year;
        _month = month;
    }

    public int ToInt()
    {
        EnsureValid();
        return _year * 100 + _month;
    }

    public static ProcessingMonth FromInt(int value) => new(value / 100, value % 100);

    public int CompareTo(ProcessingMonth other)
    {
        EnsureValid();
        other.EnsureValid();
        return (_year * 100 + _month).CompareTo(other._year * 100 + other._month);
    }

    public bool Equals(ProcessingMonth other)
    {
        EnsureValid();
        other.EnsureValid();
        return _year == other._year && _month == other._month;
    }

    public override int GetHashCode()
    {
        EnsureValid();
        return HashCode.Combine(_year, _month);
    }

    public static bool operator <(ProcessingMonth left, ProcessingMonth right) => left.CompareTo(right) < 0;
    public static bool operator <=(ProcessingMonth left, ProcessingMonth right) => left.CompareTo(right) <= 0;
    public static bool operator >(ProcessingMonth left, ProcessingMonth right) => left.CompareTo(right) > 0;
    public static bool operator >=(ProcessingMonth left, ProcessingMonth right) => left.CompareTo(right) >= 0;

    public override string ToString()
    {
        EnsureValid();
        return $"{_year:D4}-{_month:D2}";
    }

    private void EnsureValid()
    {
        if (_year is < 1900 or > 2200 || _month is < 1 or > 12)
            throw new InvalidOperationException("ProcessingMonthが初期化されていません。");
    }
}
