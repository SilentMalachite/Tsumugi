using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Tsumugi.App.Converters;

/// <summary>DateOnly ↔ string 変換コンバーター。TextBox バインド用。</summary>
public sealed class DateOnlyConverter : IValueConverter
{
    public static readonly DateOnlyConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DateOnly d ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : string.Empty;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
            return result;
        return DateOnly.FromDateTime(DateTime.Today);
    }
}
