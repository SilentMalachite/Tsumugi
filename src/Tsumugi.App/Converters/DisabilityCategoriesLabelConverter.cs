using System.Globalization;
using Avalonia.Data.Converters;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.App.Converters;

/// <summary>
/// 障害種別 (<see cref="DisabilityCategories"/>) を「身体・知的・精神・難病」のうち
/// 該当するものをカンマ区切りで表示する。該当なしは空文字。
/// </summary>
public sealed class DisabilityCategoriesLabelConverter : IValueConverter
{
    public static readonly DisabilityCategoriesLabelConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DisabilityCategories d) return string.Empty;
        var parts = new List<string>(4);
        if (d.Physical) parts.Add("身体");
        if (d.Intellectual) parts.Add("知的");
        if (d.Mental) parts.Add("精神");
        if (d.Intractable) parts.Add("難病");
        return string.Join("・", parts);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
