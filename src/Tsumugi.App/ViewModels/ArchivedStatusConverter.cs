using System.Globalization;
using Avalonia.Data.Converters;

namespace Tsumugi.App.ViewModels;

/// <summary>
/// DataGrid の「状態」列で利用する bool→表示文字列コンバータ。
/// true（アーカイブ済み）を「アーカイブ」、false（有効）を空文字に変換する。
/// </summary>
public sealed class ArchivedStatusConverter : IValueConverter
{
    public static readonly ArchivedStatusConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "アーカイブ" : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
