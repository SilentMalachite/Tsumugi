using System.Collections.ObjectModel;
using System.Reflection;
using System.Text.Json;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Claim;
using Tsumugi.Infrastructure.Csv.Specifications;

namespace Tsumugi.Infrastructure.Csv.Mapping;

public sealed class ClaimInputRequirementProvider : IClaimInputRequirementProvider
{
    private static readonly JsonSerializerOptions ReportMappingSerializerOptions = new()
    {
        AllowTrailingCommas = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
    };

    private readonly ReadOnlyCollection<ClaimInputRequirement> _requirements;

    private ClaimInputRequirementProvider(IEnumerable<ClaimInputRequirement> requirements)
    {
        _requirements = Array.AsReadOnly(requirements.ToArray());
    }

    public static ClaimInputRequirementProvider LoadEmbedded()
    {
        var catalog = CsvSpecificationLoader.LoadEmbedded();
        var csvSources = catalog.MappingByFieldId.Values
            .Where(mapping => string.Equals(mapping.Status, "missing", StringComparison.Ordinal))
            .Select(ToSource);
        using var reportMapping = OpenEmbedded(
            typeof(ClaimInputRequirementProvider).Assembly,
            "report-field-mapping-r8-06.json");
        return Create(csvSources.Concat(ReadMissingReportMappings(reportMapping)));
    }

    internal static ClaimInputRequirementProvider Create(
        IEnumerable<ClaimInputRequirementSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        var copiedSources = sources.ToArray();
        var duplicateFieldId = copiedSources
            .GroupBy(source => source.FieldId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicateFieldId is not null)
            throw new InvalidDataException("Claim requirement fieldId is duplicated.");

        var requirements = copiedSources
            .GroupBy(source => source.TargetPath, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(CreateRequirement)
            .ToArray();
        return new ClaimInputRequirementProvider(requirements);
    }

    public IReadOnlyList<ClaimInputRequirement> GetRequirements() => _requirements;

    private static ClaimInputRequirement CreateRequirement(
        IGrouping<string, ClaimInputRequirementSource> group)
    {
        if (string.IsNullOrWhiteSpace(group.Key)
            || group.Key.Split('.', StringSplitOptions.RemoveEmptyEntries).Length != 2)
            throw new InvalidDataException("Claim requirement target path is invalid.");

        var destinations = group
            .Select(source => ParseDestination(source.Destination))
            .Distinct()
            .ToArray();
        if (destinations.Length != 1)
            throw new InvalidDataException("Claim requirement target has conflicting destination values.");

        var conditions = group
            .Select(source => source.Condition)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(condition => condition, StringComparer.Ordinal)
            .Select(ParseCondition)
            .ToArray();
        var condition = conditions.Length == 1
            ? conditions[0]
            : new ClaimRequirementCondition.Any(conditions);

        return new ClaimInputRequirement(
            group.Key,
            group.Select(source => source.FieldId).Order(StringComparer.Ordinal),
            condition,
            destinations[0]);
    }

    private static ClaimInputRequirementSource ToSource(CsvFieldMapping mapping)
    {
        if (string.IsNullOrWhiteSpace(mapping.TargetModel)
            || string.IsNullOrWhiteSpace(mapping.TargetProperty))
            throw new InvalidDataException("Missing CSV mapping target is incomplete.");
        return new ClaimInputRequirementSource(
            mapping.FieldId,
            $"{mapping.TargetModel}.{mapping.TargetProperty}",
            mapping.RequiredCondition,
            mapping.UiSurface ?? string.Empty);
    }

    private static IEnumerable<ClaimInputRequirementSource> ReadMissingReportMappings(
        Stream stream)
    {
        var root = JsonSerializer.Deserialize<JsonElement>(
            stream, ReportMappingSerializerOptions);
        if (!root.TryGetProperty("mappings", out var mappings)
            || mappings.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Report field mapping does not contain a mappings array.");

        foreach (var mapping in mappings.EnumerateArray())
        {
            if (!string.Equals(RequireString(mapping, "status"), "missing", StringComparison.Ordinal))
                continue;
            yield return new ClaimInputRequirementSource(
                RequireString(mapping, "fieldId"),
                $"{RequireString(mapping, "targetModel")}.{RequireString(mapping, "targetProperty")}",
                RequireString(mapping, "requiredCondition"),
                RequireString(mapping, "uiSurface"));
        }
    }

    private static string RequireString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(property.GetString()))
            throw new InvalidDataException($"Report field mapping property '{propertyName}' is missing.");
        return property.GetString()!;
    }

    private static ClaimInputDestination ParseDestination(string value) => value switch
    {
        "CertificateView" => ClaimInputDestination.Certificate,
        "ClaimInputView" => ClaimInputDestination.ClaimInput,
        "ClaimPreparationView" => ClaimInputDestination.ClaimPreparation,
        "DailyRecordView" => ClaimInputDestination.DailyRecord,
        "OfficeView" => ClaimInputDestination.Office,
        _ => throw new InvalidDataException("Claim requirement destination is missing or unknown."),
    };

    private static ClaimRequirementCondition ParseCondition(string value)
    {
        if (string.Equals(value, "always", StringComparison.Ordinal))
            return new ClaimRequirementCondition.Always();
        if (TryUnwrap(value, "modelPresent", out var modelPresent))
            return new ClaimRequirementCondition.ModelPresent(RequireToken(modelPresent));
        if (TryUnwrap(value, "modelNonZero", out var modelNonZero))
            return new ClaimRequirementCondition.ModelNonZero(RequireToken(modelNonZero));
        if (TryUnwrap(value, "modelTrue", out var modelTrue))
            return new ClaimRequirementCondition.ModelTrue(RequireToken(modelTrue));
        if (TryUnwrap(value, "rowPresent", out var rowPresent))
            return new ClaimRequirementCondition.RowPresent(RequireToken(rowPresent));
        if (TryUnwrap(value, "modelIn", out var modelIn))
        {
            var parts = SplitTopLevel(modelIn);
            if (parts.Count < 2)
                throw UnknownCondition();
            return new ClaimRequirementCondition.ModelIn(
                RequireToken(parts[0]),
                parts.Skip(1).Select(RequireToken));
        }

        if (TryUnwrap(value, "all", out var all))
        {
            var parts = SplitTopLevel(all);
            if (parts.Count < 2)
                throw UnknownCondition();
            return new ClaimRequirementCondition.All(parts.Select(ParseCondition));
        }

        throw UnknownCondition();
    }

    private static bool TryUnwrap(string value, string function, out string inner)
    {
        var prefix = $"{function}(";
        if (value.StartsWith(prefix, StringComparison.Ordinal)
            && value.EndsWith(')'))
        {
            inner = value[prefix.Length..^1];
            return true;
        }

        inner = string.Empty;
        return false;
    }

    private static List<string> SplitTopLevel(string value)
    {
        var parts = new List<string>();
        var start = 0;
        var depth = 0;
        for (var index = 0; index < value.Length; index++)
        {
            depth += value[index] switch
            {
                '(' => 1,
                ')' => -1,
                _ => 0,
            };
            if (depth < 0)
                throw UnknownCondition();
            if (value[index] != ';' || depth != 0) continue;
            parts.Add(value[start..index]);
            start = index + 1;
        }

        if (depth != 0)
            throw UnknownCondition();
        parts.Add(value[start..]);
        return parts;
    }

    private static string RequireToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length != value.Trim().Length
            || value.Contains('(')
            || value.Contains(')'))
            throw UnknownCondition();
        return value;
    }

    private static InvalidDataException UnknownCondition() =>
        new("Claim requirement condition is unknown or malformed.");

    private static Stream OpenEmbedded(Assembly assembly, string fileName)
    {
        var resourceName = assembly.GetManifestResourceNames()
            .SingleOrDefault(name => name.EndsWith(fileName, StringComparison.Ordinal));
        return resourceName is null
            ? throw new InvalidDataException($"Embedded claim mapping '{fileName}' was not found.")
            : assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidDataException($"Embedded claim mapping '{fileName}' could not be opened.");
    }
}

internal sealed record ClaimInputRequirementSource(
    string FieldId,
    string TargetPath,
    string Condition,
    string Destination);
