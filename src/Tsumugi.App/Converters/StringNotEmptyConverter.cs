using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Tsumugi.App.Converters;

/// <summary>null/空文字のとき false を返す。IsVisible バインド用。</summary>
public sealed class StringNotEmptyConverter : IValueConverter
{
    public static readonly StringNotEmptyConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string s && !string.IsNullOrEmpty(s);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
