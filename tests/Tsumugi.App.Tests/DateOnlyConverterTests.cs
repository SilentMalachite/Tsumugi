using System.Globalization;
using Avalonia;
using Avalonia.Data;
using FluentAssertions;
using Tsumugi.App.Converters;
using Xunit;

namespace Tsumugi.App.Tests;

public sealed class DateOnlyConverterTests
{
    private static readonly DateOnlyConverter Sut = DateOnlyConverter.Instance;

    [Fact]
    public void Convert_formats_date_as_iso_yyyy_MM_dd()
    {
        var result = Sut.Convert(new DateOnly(2026, 6, 1), typeof(string), null, CultureInfo.InvariantCulture);
        result.Should().Be("2026-06-01");
    }

    [Fact]
    public void ConvertBack_parses_valid_iso_date()
    {
        var result = Sut.ConvertBack("2026-06-01", typeof(DateOnly), null, CultureInfo.InvariantCulture);
        result.Should().Be(new DateOnly(2026, 6, 1));
    }

    [Fact]
    public void ConvertBack_invalid_text_surfaces_binding_error_instead_of_today()
    {
        // 旧実装は DateTime.Today を返してしまい、DateValidator が偽の Today を有効値として通してしまった。
        var result = Sut.ConvertBack("not-a-date", typeof(DateOnly), null, CultureInfo.InvariantCulture);

        result.Should().BeOfType<BindingNotification>();
        var notification = (BindingNotification)result!;
        notification.ErrorType.Should().Be(BindingErrorType.Error);
        notification.Error.Should().NotBeNull();
    }

    [Fact]
    public void ConvertBack_invalid_text_does_not_return_today_or_any_DateOnly()
    {
        var result = Sut.ConvertBack("13月の月", typeof(DateOnly), null, CultureInfo.InvariantCulture);
        result.Should().NotBeOfType<DateOnly>();
    }

    [Fact]
    public void ConvertBack_empty_string_returns_null_to_clear_nullable_target()
    {
        var result = Sut.ConvertBack(string.Empty, typeof(DateOnly?), null, CultureInfo.InvariantCulture);
        result.Should().BeNull();
    }
}
