namespace Tsumugi.Application.Validation;

/// <summary>信頼境界の日付検証。UI の入力制限に依存しない。</summary>
public static class DateValidator
{
    private static readonly DateOnly Floor = new(1900, 1, 1);
    private static readonly DateOnly Ceiling = new(2200, 12, 31);

    public static void EnsureValid(DateOnly value, string fieldName)
    {
        if (value < Floor || value > Ceiling)
            throw new DateValidationException(
                $"日付が想定範囲外です（{Floor:yyyy-MM-dd}〜{Ceiling:yyyy-MM-dd}）。", fieldName);
    }

    public static void EnsureRange(DateOnly start, DateOnly? end, string fieldName)
    {
        EnsureValid(start, fieldName);
        if (end is not { } e) return;
        EnsureValid(e, fieldName);
        if (e < start)
            throw new DateValidationException("終了日は開始日以降である必要があります。", fieldName);
    }

    public static void EnsureYearMonth(int year, int month)
    {
        if (year < 1900 || year > 2200)
            throw new DateValidationException($"年が想定範囲外です（1900〜2200）。", "year");
        if (month < 1 || month > 12)
            throw new DateValidationException("月は1〜12の範囲で指定してください。", "month");
    }
}
