using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
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
        // 空文字は nullable な束縛先を明示的にクリアできるよう null として扱う。
        if (value is null || (value is string empty && string.IsNullOrWhiteSpace(empty)))
        {
            var underlying = Nullable.GetUnderlyingType(targetType);
            return underlying is null ? AvaloniaProperty.UnsetValue : null;
        }

        if (value is string s &&
            DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
        {
            return result;
        }

        // parse 失敗時は黙って Today を返さない。BindingNotification(Error) で UI に伝え、
        // 束縛先 (Application 層) には偽の有効値が渡らないようにする。
        return new BindingNotification(
            new FormatException($"'{value}' は yyyy-MM-dd 形式で入力してください。"),
            BindingErrorType.Error);
    }
}
