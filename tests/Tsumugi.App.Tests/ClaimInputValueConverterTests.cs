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

    private static object? ConvertBack(
        IValueConverter converter,
        string text,
        Type targetType) =>
        converter.ConvertBack(text, targetType, null, CultureInfo.InvariantCulture);
}
