using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tsumugi.Infrastructure.Csv.Specifications;

public sealed class CsvSpecificationLoader
{
    private const int SupportedSchemaVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        AllowDuplicateProperties = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private CsvSpecificationLoader()
    {
    }

    public static CsvSpecificationCatalog LoadEmbedded()
    {
        var assembly = typeof(CsvSpecificationLoader).Assembly;
        using var common = OpenEmbedded(assembly, "common-r7-10.json");
        using var provider = OpenEmbedded(assembly, "provider-claim-r7-10.json");
        using var mapping = OpenEmbedded(assembly, "field-mapping-r7-10.json");
        using var sources = OpenEmbedded(assembly, "sources.json");

        return Load(common, provider, mapping, sources);
    }

    internal static CsvSpecificationCatalog Load(
        Stream common,
        Stream provider,
        Stream mapping,
        Stream sources)
    {
        ArgumentNullException.ThrowIfNull(common);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(mapping);
        ArgumentNullException.ThrowIfNull(sources);

        var commonFile = Deserialize<CsvSpecificationFile>(common, "common");
        var providerFile = Deserialize<CsvSpecificationFile>(provider, "provider");
        var mappingFile = Deserialize<CsvMappingFile>(mapping, "mapping");
        var sourceFile = Deserialize<CsvSourceFile>(sources, "sources");

        ValidateSchemaVersion(commonFile.SchemaVersion, "common");
        ValidateSchemaVersion(providerFile.SchemaVersion, "provider");
        ValidateSchemaVersion(mappingFile.SchemaVersion, "mapping");
        ValidateSchemaVersion(sourceFile.SchemaVersion, "sources");
        ValidateNestedProperties(mappingFile, sourceFile);

        var version = CommonVersion(commonFile.SpecificationVersion);
        RequireVersion(providerFile.SpecificationVersion, $"provider-claim-{version}", "provider");
        RequireVersion(mappingFile.SpecificationVersion, $"field-mapping-{version}", "mapping");

        RejectDuplicateIds(mappingFile.Mappings, item => item.FieldId, "mapping fieldId");
        RejectDuplicateIds(sourceFile.Sources, item => item.SourceDocumentId, "sourceDocumentId");

        var mappingByFieldId = mappingFile.Mappings.ToDictionary(
            item => item.FieldId,
            StringComparer.Ordinal);
        var sourcesById = sourceFile.Sources.ToDictionary(
            item => item.SourceDocumentId,
            StringComparer.Ordinal);

        return new CsvSpecificationCatalog(
            version,
            commonFile.Records,
            providerFile.Records,
            mappingByFieldId,
            sourcesById);
    }

    private static Stream OpenEmbedded(Assembly assembly, string fileName)
    {
        var resourceName = assembly.GetManifestResourceNames()
            .SingleOrDefault(name => name.EndsWith($".Specifications.{fileName}", StringComparison.Ordinal));
        return resourceName is null
            ? throw new InvalidDataException($"Embedded CSV specification '{fileName}' was not found.")
            : assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidDataException(
                    $"Embedded CSV specification '{fileName}' could not be opened.");
    }

    private static T Deserialize<T>(Stream stream, string documentName) =>
        JsonSerializer.Deserialize<T>(stream, SerializerOptions)
        ?? throw new InvalidDataException($"CSV specification '{documentName}' is null.");

    private static void ValidateSchemaVersion(int schemaVersion, string documentName)
    {
        if (schemaVersion != SupportedSchemaVersion)
        {
            throw new InvalidDataException(
                $"CSV specification '{documentName}' has unsupported schemaVersion {schemaVersion}.");
        }
    }

    private static string CommonVersion(string specificationVersion)
    {
        const string prefix = "common-";
        if (string.IsNullOrWhiteSpace(specificationVersion)
            || !specificationVersion.StartsWith(prefix, StringComparison.Ordinal)
            || specificationVersion.Length == prefix.Length)
        {
            throw new InvalidDataException("CSV specification 'common' has an invalid specificationVersion.");
        }

        return specificationVersion[prefix.Length..];
    }

    private static void RequireVersion(
        string actual,
        string expected,
        string documentName)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"CSV specification '{documentName}' has incompatible specificationVersion.");
        }
    }

    private static void RejectDuplicateIds<T>(
        IEnumerable<T> values,
        Func<T, string> idSelector,
        string idName)
    {
        var duplicateId = values
            .GroupBy(idSelector, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicateId is not null)
        {
            throw new InvalidDataException($"Duplicate {idName} '{duplicateId}'.");
        }
    }

    private static void ValidateNestedProperties(
        CsvMappingFile mappingFile,
        CsvSourceFile sourceFile)
    {
        var liveCheckProperties = new HashSet<string>(
            ["checkedAt", "httpStatus", "responseSha256", "responseSizeBytes"],
            StringComparer.Ordinal);
        foreach (var source in sourceFile.Sources.Where(source => source.LiveCheck.HasValue))
        {
            ValidateLiveCheck(
                source.LiveCheck!.Value,
                liveCheckProperties,
                $"sourceDocumentId '{source.SourceDocumentId}' liveCheck");
        }

        var sourceContractProperties = new HashSet<string>(
            [
                "contractId",
                "itemType",
                "dateProperty",
                "lineKindProperty",
                "includedLineKinds",
                "window",
                "aggregation",
            ],
            StringComparer.Ordinal);
        foreach (var mapping in mappingFile.Mappings.Where(mapping =>
                     mapping.SourceContracts is not null))
        {
            var context = $"fieldId '{mapping.FieldId}' sourceContract";
            if (mapping.SourceContracts!.Count == 0)
            {
                throw new JsonException($"{context} array must not be empty.");
            }

            foreach (var sourceContract in mapping.SourceContracts!)
            {
                ValidateSourceContract(
                    sourceContract,
                    sourceContractProperties,
                    context);
            }
        }
    }

    private static void ValidateLiveCheck(
        JsonElement liveCheck,
        HashSet<string> allowedProperties,
        string context)
    {
        RejectUnknownProperties(liveCheck, allowedProperties, context);
        RequireNonBlankString(liveCheck, "checkedAt", context);
        RequireLowercaseSha256(liveCheck, "responseSha256", context);

        var httpStatus = RequireProperty(liveCheck, "httpStatus", context);
        if (!httpStatus.TryGetInt32(out _))
        {
            throw new JsonException($"{context} property 'httpStatus' must be an integer.");
        }

        var responseSizeBytes = RequireProperty(liveCheck, "responseSizeBytes", context);
        if (!responseSizeBytes.TryGetInt64(out var sizeBytes) || sizeBytes <= 0)
        {
            throw new JsonException(
                $"{context} property 'responseSizeBytes' must be a positive integer.");
        }
    }

    private static void ValidateSourceContract(
        JsonElement sourceContract,
        HashSet<string> allowedProperties,
        string context)
    {
        RejectUnknownProperties(sourceContract, allowedProperties, context);
        foreach (var propertyName in new[]
                 {
                     "contractId",
                     "itemType",
                     "dateProperty",
                     "lineKindProperty",
                     "aggregation",
                 })
        {
            RequireNonBlankString(sourceContract, propertyName, context);
        }

        var includedLineKinds = RequireProperty(
            sourceContract,
            "includedLineKinds",
            context);
        if (includedLineKinds.ValueKind != JsonValueKind.Array
            || includedLineKinds.GetArrayLength() == 0
            || includedLineKinds.EnumerateArray().Any(lineKind =>
                lineKind.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(lineKind.GetString())))
        {
            throw new JsonException(
                $"{context} property 'includedLineKinds' must be a non-empty string array.");
        }

        if (sourceContract.TryGetProperty("window", out var window)
            && (window.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(window.GetString())))
        {
            throw new JsonException($"{context} property 'window' must be a non-empty string.");
        }
    }

    private static JsonElement RequireProperty(
        JsonElement element,
        string propertyName,
        string context)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            throw new JsonException($"{context} is missing property '{propertyName}'.");
        }

        return property;
    }

    private static void RequireNonBlankString(
        JsonElement element,
        string propertyName,
        string context)
    {
        var property = RequireProperty(element, propertyName, context);
        if (property.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new JsonException(
                $"{context} property '{propertyName}' must be a non-empty string.");
        }
    }

    private static void RequireLowercaseSha256(
        JsonElement element,
        string propertyName,
        string context)
    {
        var property = RequireProperty(element, propertyName, context);
        var value = property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
        if (value is null
            || value.Length != 64
            || value.Any(character => character is not (>= '0' and <= '9')
                and not (>= 'a' and <= 'f')))
        {
            throw new JsonException(
                $"{context} property '{propertyName}' must be lowercase hexadecimal sha256.");
        }
    }

    private static void RejectUnknownProperties(
        JsonElement element,
        HashSet<string> allowedProperties,
        string context)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException($"Expected a JSON object for {context}.");
        }

        var unknownPropertyName = element.EnumerateObject()
            .Select(property => property.Name)
            .FirstOrDefault(name => !allowedProperties.Contains(name));
        if (unknownPropertyName is not null)
        {
            throw new JsonException(
                $"Unknown JSON property '{unknownPropertyName}' in {context}.");
        }
    }

    private sealed record CsvSpecificationFile(
        int SchemaVersion,
        string SpecificationVersion,
        IReadOnlyList<CsvRecordSpecification> Records,
        string? Encoding = null,
        string? LineEnding = null,
        string? ServiceScope = null);

    private sealed record CsvMappingFile(
        int SchemaVersion,
        string SpecificationVersion,
        IReadOnlyList<CsvFieldMapping> Mappings);

    private sealed record CsvSourceFile(
        int SchemaVersion,
        IReadOnlyList<CsvSourceDocument> Sources);
}
