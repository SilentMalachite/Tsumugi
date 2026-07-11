using System.Reflection;
using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Claim;

namespace Tsumugi.Application.Tests.Claim;

public sealed class ClaimFinalizationContractTests
{
    private static readonly string[] AllowedStringParameters =
    [
        "createdBy",
        "operationApplicationVersion",
        "claimMasterVersion",
        "csvSpecificationVersion",
        "reportSpecificationVersion",
        "snapshotApplicationVersion",
        "snapshotSchemaVersion",
    ];

    [Fact]
    public void Draft_public_API_has_no_raw_snapshot_json_string_or_byte_input()
    {
        var parameters = new[]
        {
            typeof(ClaimFinalizationDraft),
            typeof(ClaimFinalizationDetailDraft),
        }
            .SelectMany(type => type.GetConstructors(BindingFlags.Instance | BindingFlags.Public))
            .SelectMany(constructor => constructor.GetParameters())
            .ToArray();

        parameters.Should().NotContain(parameter =>
            parameter.ParameterType == typeof(byte[])
            || parameter.ParameterType == typeof(ReadOnlyMemory<byte>)
            || parameter.ParameterType == typeof(Memory<byte>)
            || parameter.Name!.Contains("json", StringComparison.OrdinalIgnoreCase)
            || parameter.Name.Contains("raw", StringComparison.OrdinalIgnoreCase));
        parameters.Where(parameter => parameter.ParameterType == typeof(string))
            .Select(parameter => parameter.Name)
            .Should().OnlyContain(name => AllowedStringParameters.Contains(name, StringComparer.OrdinalIgnoreCase));
        parameters.Where(parameter => parameter.Name!.Contains("snapshot", StringComparison.OrdinalIgnoreCase))
            .Where(parameter => !parameter.Name!.EndsWith("Version", StringComparison.OrdinalIgnoreCase))
            .Should().OnlyContain(parameter =>
                parameter.ParameterType == typeof(ValidatedClaimSnapshotEnvelope));
    }

    [Fact]
    public void ClaimErrorCode_is_a_closed_explicit_enum()
    {
        Enum.GetValues<ClaimErrorCode>().Should().Equal(
            ClaimErrorCode.InvalidOperationPayload,
            ClaimErrorCode.InvalidSnapshotEnvelope,
            ClaimErrorCode.UnsupportedOperationSchema,
            ClaimErrorCode.UnsupportedSnapshotCodec,
            ClaimErrorCode.InvalidHistory,
            ClaimErrorCode.ExpectedHeadMismatch,
            ClaimErrorCode.OperationIdCollision,
            ClaimErrorCode.PersistenceFailure);
        Enum.GetUnderlyingType(typeof(ClaimErrorCode)).Should().Be<int>();
    }

    [Fact]
    public void ClaimJsonPath_allows_only_property_tokens_and_array_indexes()
    {
        typeof(ClaimJsonPathSegment)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public)
            .Should().BeEmpty();
        typeof(ClaimJsonPathSegment)
            .GetNestedTypes(BindingFlags.Public)
            .Should().BeEquivalentTo([
                typeof(ClaimJsonPathSegment.PropertyToken),
                typeof(ClaimJsonPathSegment.ArrayIndex),
            ]);

        var path = new ClaimJsonPath([
            new ClaimJsonPathSegment.PropertyToken("details"),
            new ClaimJsonPathSegment.ArrayIndex(0),
        ]);

        path.Segments.Should().ContainInOrder(
            new ClaimJsonPathSegment.PropertyToken("details"),
            new ClaimJsonPathSegment.ArrayIndex(0));
        FluentActions.Invoking(() => new ClaimJsonPathSegment.PropertyToken("details[0]"))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new ClaimJsonPathSegment.ArrayIndex(-1))
            .Should().Throw<ArgumentOutOfRangeException>();
    }
}
