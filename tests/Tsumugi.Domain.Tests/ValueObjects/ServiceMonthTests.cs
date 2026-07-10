using System.Reflection;
using FluentAssertions;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Tests.ValueObjects;

public sealed class ServiceMonthTests
{
    [Theory]
    [InlineData(1900, 1, 190001, "1900-01")]
    [InlineData(2026, 6, 202606, "2026-06")]
    [InlineData(2200, 12, 220012, "2200-12")]
    public void Constructor_ToInt_FromInt_and_ToString_round_trip(
        int year,
        int month,
        int compactValue,
        string text)
    {
        var value = new ServiceMonth(year, month);

        value.Year.Should().Be(year);
        value.Month.Should().Be(month);
        value.ToInt().Should().Be(compactValue);
        ServiceMonth.FromInt(compactValue).Should().Be(value);
        value.ToString().Should().Be(text);
    }

    [Theory]
    [InlineData(1899, 12)]
    [InlineData(2201, 1)]
    [InlineData(2026, 0)]
    [InlineData(2026, 13)]
    public void Constructor_rejects_out_of_range_values(int year, int month)
        => FluentActions.Invoking(() => new ServiceMonth(year, month))
            .Should().Throw<ArgumentOutOfRangeException>();

    [Theory]
    [InlineData(189912)]
    [InlineData(220101)]
    [InlineData(202600)]
    [InlineData(202613)]
    public void FromInt_rejects_out_of_range_values(int value)
        => FluentActions.Invoking(() => ServiceMonth.FromInt(value))
            .Should().Throw<ArgumentOutOfRangeException>();

    [Fact]
    public void Comparison_contract_is_chronological()
    {
        var earlier = new ServiceMonth(2025, 12);
        var later = new ServiceMonth(2026, 1);
        var same = new ServiceMonth(2025, 12);

        earlier.CompareTo(later).Should().BeNegative();
        later.CompareTo(earlier).Should().BePositive();
        earlier.CompareTo(same).Should().Be(0);
        (earlier < later).Should().BeTrue();
        (earlier <= same).Should().BeTrue();
        (later > earlier).Should().BeTrue();
        (same >= earlier).Should().BeTrue();
    }

    [Fact]
    public void Does_not_expose_conversions_to_other_month_types()
    {
        var forbiddenTypes = new[]
        {
            typeof(ServiceMonth),
            typeof(ProcessingMonth),
            typeof(YearMonth),
        };

        var conversions = forbiddenTypes
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(method => method.Name is "op_Implicit" or "op_Explicit")
            .Where(method => method.ReturnType == typeof(ServiceMonth)
                || method.GetParameters().Any(parameter => parameter.ParameterType == typeof(ServiceMonth)));

        conversions.Should().BeEmpty();
    }

    [Fact]
    public void Default_value_fails_closed_for_public_contract()
    {
        var invalid = default(ServiceMonth);
        var valid = new ServiceMonth(2026, 6);
        Action[] actions =
        [
            () => _ = invalid.Year,
            () => _ = invalid.Month,
            () => _ = invalid.ToInt(),
            () => _ = invalid.ToString(),
            () => _ = invalid.CompareTo(valid),
            () => _ = valid.CompareTo(invalid),
            () => _ = invalid.Equals(valid),
            () => _ = valid.Equals(invalid),
            () => _ = invalid.GetHashCode(),
            () => _ = invalid == default,
            () => _ = invalid != valid,
            () => _ = invalid < valid,
            () => _ = invalid <= valid,
            () => _ = invalid > valid,
            () => _ = invalid >= valid,
        ];

        foreach (var action in actions)
            action.Should().Throw<InvalidOperationException>();
    }
}
