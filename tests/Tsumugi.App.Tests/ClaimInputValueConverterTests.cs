using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using FluentAssertions;
using Tsumugi.App.Converters;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.App.Tests;

public sealed class ClaimInputValueConverterTests
{
    [Fact]
    public void Claim_specific_text_formats_parse_to_typed_values()
    {
        ConvertBack(ClaimMasterVersionConverter.Instance, "master-v1",
                typeof(ClaimMasterVersion?))
            .Should().Be(new ClaimMasterVersion("master-v1"));
        ConvertBack(AverageWageBandOptionConverter.Instance, "Numeric:12",
                typeof(AverageWageBandOption?))
            .Should().Be(new AverageWageBandOption(AverageWageBandOptionKind.Numeric, 12));
        ConvertBack(VersionedAverageWageBandOptionConverter.Instance,
                "master-v1|FiledTransition:3", typeof(VersionedAverageWageBandOption?))
            .Should().Be(new VersionedAverageWageBandOption(
                new ClaimMasterVersion("master-v1"),
                new AverageWageBandOption(AverageWageBandOptionKind.FiledTransition, 3)));
        ConvertBack(ServiceMonthConverter.Instance, "2026-06", typeof(ServiceMonth?))
            .Should().Be(new ServiceMonth(2026, 6));
        ConvertBack(DateRangeConverter.Instance, "2026-06-01..2027-05-31",
                typeof(DateRange?))
            .Should().Be(new DateRange(
                new DateOnly(2026, 6, 1), new DateOnly(2027, 5, 31)));
        ConvertBack(DateTimeOffsetConverter.Instance, "2026-07-12T10:30:00+09:00",
                typeof(DateTimeOffset?))
            .Should().Be(new DateTimeOffset(2026, 7, 12, 10, 30, 0, TimeSpan.FromHours(9)));
    }

    [Theory]
    [MemberData(nameof(InvalidInputs))]
    public void Invalid_text_surfaces_binding_error_without_mutating_typed_draft(
        IValueConverter converter,
        string text,
        Type targetType)
    {
        ArgumentNullException.ThrowIfNull(converter);
        var result = ConvertBack(converter, text, targetType);

        result.Should().BeOfType<BindingNotification>();
        ((BindingNotification)result!).ErrorType.Should().Be(BindingErrorType.Error);
    }

    public static TheoryData<IValueConverter, string, Type> InvalidInputs => new()
    {
        { ClaimMasterVersionConverter.Instance, " padded ", typeof(ClaimMasterVersion?) },
        { AverageWageBandOptionConverter.Instance, "Unknown:1", typeof(AverageWageBandOption?) },
        { VersionedAverageWageBandOptionConverter.Instance, "master-v1:Numeric:1", typeof(VersionedAverageWageBandOption?) },
        { ServiceMonthConverter.Instance, "2026-13", typeof(ServiceMonth?) },
        { DateRangeConverter.Instance, "2026-06-02..2026-06-01", typeof(DateRange?) },
        { DateTimeOffsetConverter.Instance, "tomorrow", typeof(DateTimeOffset?) },
    };

    [Theory]
    [InlineData("1900-01", 1900, 1)]
    [InlineData("2200-12", 2200, 12)]
    public void Service_month_accepts_domain_year_boundaries(
        string text,
        int expectedYear,
        int expectedMonth)
    {
        var result = ConvertBack(ServiceMonthConverter.Instance, text, typeof(ServiceMonth?));

        result.Should().Be(new ServiceMonth(expectedYear, expectedMonth));
    }

    [Theory]
    [InlineData("1800-01")]
    [InlineData("2201-01")]
    public void Service_month_outside_domain_years_surfaces_binding_error(string text)
    {
        var action = () => ConvertBack(
            ServiceMonthConverter.Instance, text, typeof(ServiceMonth?));

        var result = action.Should().NotThrow().Which;
        result.Should().BeOfType<BindingNotification>();
        ((BindingNotification)result!).ErrorType.Should().Be(BindingErrorType.Error);
    }

    [Theory]
    [InlineData("2026-07-12T10:30:00+09:00", 0)]
    [InlineData("2026-07-12T10:30:00.1234567+09:00", 1_234_567)]
    public void Date_time_offset_accepts_explicit_iso_with_offset(
        string text,
        int expectedTicksWithinSecond)
    {
        var result = ConvertBack(
            DateTimeOffsetConverter.Instance, text, typeof(DateTimeOffset?));

        result.Should().BeOfType<DateTimeOffset>();
        var parsed = (DateTimeOffset)result!;
        parsed.Offset.Should().Be(TimeSpan.FromHours(9));
        (parsed.Ticks % TimeSpan.TicksPerSecond).Should().Be(expectedTicksWithinSecond);
    }

    [Theory]
    [InlineData("07/12/2026 10:30:00 +09:00")]
    [InlineData("2026年7月12日 10時30分00秒 +09:00")]
    [InlineData("2026-07-12 10:30:00 +09:00")]
    public void Date_time_offset_rejects_non_iso_text(string text)
    {
        var result = ConvertBack(
            DateTimeOffsetConverter.Instance, text, typeof(DateTimeOffset?));

        result.Should().BeOfType<BindingNotification>();
        ((BindingNotification)result!).ErrorType.Should().Be(BindingErrorType.Error);
    }

    [Fact]
    public void Date_time_offset_convert_roundtrips_and_preserves_offset()
    {
        var source = new DateTimeOffset(
            2026, 7, 12, 10, 30, 0, 123, TimeSpan.FromHours(9)).AddTicks(4_567);
        var text = DateTimeOffsetConverter.Instance.Convert(
            source, typeof(string), null, CultureInfo.InvariantCulture);

        var result = DateTimeOffsetConverter.Instance.ConvertBack(
            text, typeof(DateTimeOffset?), null, CultureInfo.InvariantCulture);

        result.Should().Be(source);
        ((DateTimeOffset)result!).Offset.Should().Be(source.Offset);
    }

    [Fact]
    public void Date_time_offset_null_and_empty_are_safe_nullable_values()
    {
        DateTimeOffsetConverter.Instance.Convert(
                null, typeof(string), null, CultureInfo.InvariantCulture)
            .Should().Be(string.Empty);
        DateTimeOffsetConverter.Instance.ConvertBack(
                string.Empty, typeof(DateTimeOffset?), null, CultureInfo.InvariantCulture)
            .Should().BeNull();
    }

    private static object? ConvertBack(
        IValueConverter converter,
        string text,
        Type targetType) =>
        converter.ConvertBack(text, targetType, null, CultureInfo.InvariantCulture);
}
