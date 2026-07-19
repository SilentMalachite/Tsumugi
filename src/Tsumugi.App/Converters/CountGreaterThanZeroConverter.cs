using System.Globalization;
using Avalonia.Data.Converters;

namespace Tsumugi.App.Converters;

/// <summary>件数（int）が1件以上のとき true。IsVisible バインド用（一覧の空表示切替）。</summary>
public sealed class CountGreaterThanZeroConverter : IValueConverter
{
    public static readonly CountGreaterThanZeroConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int count && count > 0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
