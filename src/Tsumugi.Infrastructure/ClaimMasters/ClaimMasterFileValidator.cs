using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Infrastructure.ClaimMasters;

internal static class ClaimMasterFileValidator
{
    private const string SupportedSchemaVersion = "1";

    private static readonly IReadOnlyDictionary<string, string> ExpectedFiles =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["basic-rewards.json"] = "basic-rewards",
            ["additions.json"] = "additions",
            ["region-unit-prices.json"] = "region-unit-prices",
            ["burden-caps.json"] = "burden-caps",
            ["transition-rules.json"] = "transition-rules",
            ["service-codes.json"] = "service-codes",
        };

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        AllowDuplicateProperties = false,
        PropertyNameCaseInsensitive = false,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    internal static void ValidateAll(
        IReadOnlyDictionary<string, Stream> masterFiles,
        IReadOnlySet<string> knownSourceDocumentIds)
    {
        ArgumentNullException.ThrowIfNull(masterFiles);
        ArgumentNullException.ThrowIfNull(knownSourceDocumentIds);

        var missing = ExpectedFiles.Keys.Except(masterFiles.Keys, StringComparer.Ordinal).ToArray();
        var extra = masterFiles.Keys.Except(ExpectedFiles.Keys, StringComparer.Ordinal).ToArray();
        if (missing.Length != 0 || extra.Length != 0)
        {
            throw new InvalidDataException(
                $"Claim master filenames must match exactly. Missing: [{string.Join(", ", missing)}]; extra: [{string.Join(", ", extra)}].");
        }

        foreach (var expected in ExpectedFiles)
        {
            var stream = masterFiles[expected.Key];
            if (stream is null)
                throw new ArgumentException($"Claim master stream '{expected.Key}' cannot be null.", nameof(masterFiles));
            if (!stream.CanRead)
                throw new ArgumentException($"Claim master stream '{expected.Key}' must be readable.", nameof(masterFiles));

            var file = Deserialize(stream, expected.Key);
            ValidateFile(expected.Key, expected.Value, file, knownSourceDocumentIds);
        }
    }

    private static MasterFile Deserialize(Stream stream, string fileName)
    {
        try
        {
            return JsonSerializer.Deserialize<MasterFile>(stream, SerializerOptions)
                ?? throw new InvalidDataException($"Claim master file '{fileName}' is null.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"Claim master file '{fileName}' contains invalid JSON.",
                exception);
        }
    }

    private static void ValidateFile(
        string fileName,
        string expectedKind,
        MasterFile file,
        IReadOnlySet<string> knownSourceDocumentIds)
    {
        if (!string.Equals(file.SchemaVersion, SupportedSchemaVersion, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Claim master file '{fileName}' has unsupported schemaVersion '{file.SchemaVersion}'.");
        }

        if (!string.Equals(file.MasterKind, expectedKind, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Claim master file '{fileName}' must have masterKind '{expectedKind}'.");
        }

        ArgumentNullException.ThrowIfNull(file.Entries);
        if (file.Entries.Any(entry => entry is null))
            throw new InvalidDataException($"Claim master file '{fileName}' cannot contain null entries.");

        var parsed = file.Entries.Select(entry => ParseEntry(fileName, entry, knownSourceDocumentIds)).ToArray();
        var duplicate = parsed
            .GroupBy(entry => (entry.Key, entry.EffectiveFrom))
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicate is not null)
        {
            throw new InvalidDataException(
                $"Claim master file '{fileName}' has duplicate key '{duplicate.Value.Key}' and effectiveFrom '{duplicate.Value.EffectiveFrom}'.");
        }

        foreach (var group in parsed.GroupBy(entry => entry.Key, StringComparer.Ordinal))
            ValidatePeriods(fileName, group.Key, group.OrderBy(entry => entry.EffectiveFrom).ToArray());
    }

    private static ParsedEntry ParseEntry(
        string fileName,
        MasterEntry entry,
        IReadOnlySet<string> knownSourceDocumentIds)
    {
        ValidateRequiredText(entry.Key, "key", fileName);
        ValidateRequiredText(entry.SourceDocumentId, "sourceDocumentId", fileName);
        if (!knownSourceDocumentIds.Contains(entry.SourceDocumentId))
        {
            throw new InvalidDataException(
                $"Claim master file '{fileName}' refers to unknown sourceDocumentId '{entry.SourceDocumentId}'.");
        }

        var effectiveFrom = ParseMonth(entry.EffectiveFrom, "effectiveFrom", fileName);
        var effectiveTo = entry.EffectiveTo is null
            ? (ServiceMonth?)null
            : ParseMonth(entry.EffectiveTo, "effectiveTo", fileName);
        if (effectiveTo is { } end && end < effectiveFrom)
        {
            throw new InvalidDataException(
                $"Claim master file '{fileName}' key '{entry.Key}' has a reversed effective range.");
        }

        if (entry.Values.ValueKind != JsonValueKind.Object
            || !entry.Values.EnumerateObject().Any())
        {
            throw new InvalidDataException(
                $"Claim master file '{fileName}' key '{entry.Key}' values must be a non-empty object.");
        }

        return new ParsedEntry(entry.Key, effectiveFrom, effectiveTo);
    }

    private static void ValidatePeriods(
        string fileName,
        string key,
        IReadOnlyList<ParsedEntry> entries)
    {
        for (var index = 0; index < entries.Count - 1; index++)
        {
            var current = entries[index];
            var next = entries[index + 1];
            if (current.EffectiveTo is null)
            {
                throw new InvalidDataException(
                    $"Claim master file '{fileName}' key '{key}' has entries after an open-ended range.");
            }

            if (next.EffectiveFrom <= current.EffectiveTo.Value)
            {
                throw new InvalidDataException(
                    $"Claim master file '{fileName}' key '{key}' has overlapping ranges.");
            }

            var expectedNext = NextMonth(current.EffectiveTo.Value);
            if (next.EffectiveFrom != expectedNext)
            {
                throw new InvalidDataException(
                    $"Claim master file '{fileName}' key '{key}' has a gap at '{expectedNext}'.");
            }
        }

        if (entries[^1].EffectiveTo is { } lastEnd)
        {
            throw new InvalidDataException(
                $"Claim master file '{fileName}' key '{key}' ends at '{lastEnd}' and leaves an implicit future gap.");
        }
    }

    private static ServiceMonth ParseMonth(string value, string propertyName, string fileName)
    {
        if (value.Length != 7
            || value[4] != '-'
            || !int.TryParse(value.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var year)
            || !int.TryParse(value.AsSpan(5, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var month)
            || year is < 1900 or > 2200
            || month is < 1 or > 12)
        {
            throw new InvalidDataException(
                $"Claim master file '{fileName}' property '{propertyName}' must be YYYY-MM.");
        }

        return new ServiceMonth(year, month);
    }

    private static ServiceMonth NextMonth(ServiceMonth month) => month.Month == 12
        ? new ServiceMonth(month.Year + 1, 1)
        : new ServiceMonth(month.Year, month.Month + 1);

    private static void ValidateRequiredText(string value, string propertyName, string fileName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != value.Trim().Length)
        {
            throw new InvalidDataException(
                $"Claim master file '{fileName}' property '{propertyName}' must be non-blank without outer whitespace.");
        }
    }

    private sealed record MasterFile(
        string SchemaVersion,
        string MasterKind,
        IReadOnlyList<MasterEntry> Entries);

    private sealed record MasterEntry(
        string Key,
        string EffectiveFrom,
        string? EffectiveTo,
        string SourceDocumentId,
        JsonElement Values);

    private sealed record ParsedEntry(
        string Key,
        ServiceMonth EffectiveFrom,
        ServiceMonth? EffectiveTo);
}
