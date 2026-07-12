using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.App.Converters;

public sealed class ClaimMasterVersionConverter : IValueConverter
{
    public static readonly ClaimMasterVersionConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is ClaimMasterVersion version ? version.Value : string.Empty;

    public object? ConvertBack(
        object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (ClaimInputConverter.TryBlank(value, targetType, out var blank)) return blank;
        if (value is not string text)
            return ClaimInputConverter.Error("請求master版を文字列で入力してください。");
        try
        {
            return new ClaimMasterVersion(text);
        }
        catch (ArgumentException ex)
        {
            return ClaimInputConverter.Error(ex);
        }
    }
}

public sealed class AverageWageBandOptionConverter : IValueConverter
{
    public static readonly AverageWageBandOptionConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is AverageWageBandOption option
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"{option.Kind}:{option.OfficialOptionCode}")
            : string.Empty;

    public object? ConvertBack(
        object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (ClaimInputConverter.TryBlank(value, targetType, out var blank)) return blank;
        return value is string text && ClaimInputConverter.TryOption(text, out var option)
            ? option
            : ClaimInputConverter.Error("平均工賃optionは Kind:Code 形式で入力してください。");
    }
}

public sealed class VersionedAverageWageBandOptionConverter : IValueConverter
{
    public static readonly VersionedAverageWageBandOptionConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is VersionedAverageWageBandOption option
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"{option.MasterVersion.Value}|{option.Option.Kind}:{option.Option.OfficialOptionCode}")
            : string.Empty;

    public object? ConvertBack(
        object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (ClaimInputConverter.TryBlank(value, targetType, out var blank)) return blank;
        if (value is not string text)
            return ClaimInputConverter.Error("版付き平均工賃optionを文字列で入力してください。");
        var parts = text.Split('|', StringSplitOptions.None);
        if (parts.Length != 2 || !ClaimInputConverter.TryOption(parts[1], out var option))
        {
            return ClaimInputConverter.Error(
                "版付き平均工賃optionは Version|Kind:Code 形式で入力してください。");
        }

        try
        {
            return new VersionedAverageWageBandOption(
                new ClaimMasterVersion(parts[0]), option);
        }
        catch (ArgumentException ex)
        {
            return ClaimInputConverter.Error(ex);
        }
    }
}

public sealed class ServiceMonthConverter : IValueConverter
{
    public static readonly ServiceMonthConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is ServiceMonth month
            ? string.Create(CultureInfo.InvariantCulture, $"{month.Year:D4}-{month.Month:D2}")
            : string.Empty;

    public object? ConvertBack(
        object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (ClaimInputConverter.TryBlank(value, targetType, out var blank)) return blank;
        if (value is string text && DateOnly.TryParseExact( // InvariantCulture is supplied below.
                $"{text}-01", "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date))
        {
            try
            {
                return new ServiceMonth(date.Year, date.Month);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                return ClaimInputConverter.Error(ex);
            }
        }

        return ClaimInputConverter.Error("年月は yyyy-MM 形式で入力してください。");
    }
}

public sealed class DateRangeConverter : IValueConverter
{
    public static readonly DateRangeConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DateRange range
            ? string.Concat(
                range.Start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                "..",
                range.End?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            : string.Empty;

    public object? ConvertBack(
        object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (ClaimInputConverter.TryBlank(value, targetType, out var blank)) return blank;
        if (value is not string text)
            return ClaimInputConverter.Error("期間を文字列で入力してください。");
        var parts = text.Split("..", StringSplitOptions.None);
        if (parts.Length != 2 || !ClaimInputConverter.TryDate(parts[0], out var start)
            || parts[1].Length > 0 && !ClaimInputConverter.TryDate(parts[1], out _))
        {
            return ClaimInputConverter.Error(
                "期間は yyyy-MM-dd..yyyy-MM-dd 形式で入力してください。");
        }

        DateOnly? end = parts[1].Length == 0
            ? null
            : DateOnly.ParseExact(parts[1], "yyyy-MM-dd", CultureInfo.InvariantCulture);
        try
        {
            return new DateRange(start, end);
        }
        catch (ArgumentException ex)
        {
            return ClaimInputConverter.Error(ex);
        }
    }
}

public sealed class DateTimeOffsetConverter : IValueConverter
{
    private static readonly string[] AcceptedFormats =
    [
        "yyyy-MM-dd'T'HH:mm:sszzz",
        "yyyy-MM-dd'T'HH:mm:ss.fzzz",
        "yyyy-MM-dd'T'HH:mm:ss.ffzzz",
        "yyyy-MM-dd'T'HH:mm:ss.fffzzz",
        "yyyy-MM-dd'T'HH:mm:ss.ffffzzz",
        "yyyy-MM-dd'T'HH:mm:ss.fffffzzz",
        "yyyy-MM-dd'T'HH:mm:ss.ffffffzzz",
        "yyyy-MM-dd'T'HH:mm:ss.fffffffzzz",
    ];

    public static readonly DateTimeOffsetConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DateTimeOffset dateTime
            ? dateTime.ToString("O", CultureInfo.InvariantCulture)
            : string.Empty;

    public object? ConvertBack(
        object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (ClaimInputConverter.TryBlank(value, targetType, out var blank)) return blank;
        return value is string text && DateTimeOffset.TryParseExact( // InvariantCulture is supplied below.
            text, AcceptedFormats, CultureInfo.InvariantCulture, DateTimeStyles.None,
            out var dateTime)
            ? dateTime
            : ClaimInputConverter.Error("日時はISO 8601形式で入力してください。");
    }
}

internal static class ClaimInputConverter
{
    public static bool TryBlank(object? value, Type targetType, out object? blank)
    {
        if (value is not null
            && (value is not string text || !string.IsNullOrWhiteSpace(text)))
        {
            blank = null;
            return false;
        }

        blank = Nullable.GetUnderlyingType(targetType) is null
            ? AvaloniaProperty.UnsetValue
            : null;
        return true;
    }

    public static bool TryOption(string value, out AverageWageBandOption option)
    {
        option = default;
        var parts = value.Split(':', StringSplitOptions.None);
        if (parts.Length != 2
            || !Enum.TryParse<AverageWageBandOptionKind>(parts[0], ignoreCase: true, out var kind)
            || kind == AverageWageBandOptionKind.Unknown
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var code))
            return false;

        try
        {
            option = new AverageWageBandOption(kind, code);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public static bool TryDate(string value, out DateOnly date) =>
        DateOnly.TryParseExact( // InvariantCulture is supplied below.
            value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out date);

    public static BindingNotification Error(string message) =>
        Error(new FormatException(message));

    public static BindingNotification Error(Exception error) =>
        new(error, BindingErrorType.Error);
}
