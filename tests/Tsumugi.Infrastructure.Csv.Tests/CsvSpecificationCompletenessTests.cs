using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Tsumugi.Infrastructure.Csv;

namespace Tsumugi.Infrastructure.Csv.Tests;

public sealed class CsvSpecificationCompletenessTests
{
    private static readonly Assembly CsvAssembly = typeof(CsvAssemblyMarker).Assembly;

    private static readonly Dictionary<string, int> ExpectedRecordFieldCounts =
        new Dictionary<string, int>
        {
            ["common:outer:control"] = 12,
            ["common:outer:data"] = 4,
            ["common:outer:end"] = 3,
            ["provider:J111:01"] = 23,
            ["provider:J111:02"] = 14,
            ["provider:J121:01"] = 35,
            ["provider:J121:02"] = 12,
            ["provider:J121:03"] = 11,
            ["provider:J121:04"] = 33,
            ["provider:J121:05"] = 11,
            ["provider:J611:01"] = 172,
            ["provider:J611:02"] = 113,
        };

    [Fact]
    public void Embedded_specifications_contain_every_official_record_and_field()
    {
        using var sources = ReadEmbeddedJson("sources.json");
        using var common = ReadEmbeddedJson("common-r7-10.json");
        using var provider = ReadEmbeddedJson("provider-claim-r7-10.json");

        var sourceIds = sources.RootElement.GetProperty("sources")
            .EnumerateArray()
            .Select(source => source.GetProperty("sourceDocumentId").GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        var records = common.RootElement.GetProperty("records").EnumerateArray()
            .Concat(provider.RootElement.GetProperty("records").EnumerateArray())
            .ToArray();

        var failures = new List<string>();
        var recordIds = records.Select(record => record.GetProperty("recordId").GetString()!).ToArray();
        failures.AddRange(Duplicates(recordIds).Select(id => $"duplicate recordId: {id}"));

        foreach (var expected in ExpectedRecordFieldCounts)
        {
            var matching = records.Where(record =>
                record.GetProperty("recordId").GetString() == expected.Key).ToArray();
            if (matching.Length == 0)
            {
                failures.Add($"missing recordId: {expected.Key}");
                continue;
            }

            var fields = matching[0].GetProperty("fields").EnumerateArray().ToArray();
            if (fields.Length != expected.Value)
            {
                failures.Add($"{expected.Key}: expected {expected.Value} fields but found {fields.Length}");
            }

            var positions = fields.Select(field => field.GetProperty("position").GetInt32()).ToArray();
            var expectedPositions = Enumerable.Range(1, fields.Length).ToArray();
            if (!positions.SequenceEqual(expectedPositions))
            {
                failures.Add($"{expected.Key}: positions must be 1..{fields.Length}");
            }

            var fieldIds = fields.Select(field => field.GetProperty("fieldId").GetString()!).ToArray();
            failures.AddRange(Duplicates(fieldIds).Select(id => $"duplicate fieldId: {id}"));

            var sourceDocumentId = matching[0].GetProperty("sourceDocumentId").GetString()!;
            if (!sourceIds.Contains(sourceDocumentId))
            {
                failures.Add($"{expected.Key}: unresolved sourceDocumentId {sourceDocumentId}");
            }

            if (matching[0].GetProperty("sourcePage").GetInt32() <= 0)
            {
                failures.Add($"{expected.Key}: sourcePage must be positive");
            }

            foreach (var field in fields)
            {
                var fieldId = field.GetProperty("fieldId").GetString()!;
                if (field.GetProperty("maxBytes").GetInt32() <= 0)
                {
                    failures.Add($"{fieldId}: maxBytes must be positive");
                }

                foreach (var property in new[] { "officialName", "requiredWhen", "dataType", "quoteRule" })
                {
                    if (string.IsNullOrWhiteSpace(field.GetProperty(property).GetString()))
                    {
                        failures.Add($"{fieldId}: {property} is blank");
                    }
                }

                field.GetProperty("allowedCodes").ValueKind.Should().Be(JsonValueKind.Array, fieldId);
            }
        }

        records.Length.Should().Be(ExpectedRecordFieldCounts.Count);
        records.Sum(record => record.GetProperty("fields").GetArrayLength()).Should().Be(443);
        var allFields = records.SelectMany(record => record.GetProperty("fields").EnumerateArray())
            .ToDictionary(field => field.GetProperty("fieldId").GetString()!, StringComparer.Ordinal);
        AssertAllowedCodes(allFields, "provider:J111:02:006", "1", "2", "3");
        AssertAllowedCodes(allFields, "provider:J121:01:011", "1");
        AssertAllowedCodes(allFields, "provider:J121:01:013", "1");
        AssertAllowedCodes(allFields, "provider:J121:01:016", "1", "2", "3");
        AssertAllowedCodes(allFields, "provider:J121:04:008", "1");
        AssertAllowedCodes(allFields, "provider:J611:02:029", "1");
        AssertAllowedCodes(allFields, "provider:J611:02:032", "1");
        AssertAllowedCodes(allFields, "provider:J611:02:036", "8");
        AssertAllowedCodes(allFields, "provider:J611:02:074", "1", "2", "3", "4", "6");
        AssertAllowedCodes(allFields, "provider:J611:02:083", "1", "2");
        AssertAllowedCodes(allFields, "provider:J611:02:096", "1");
        AssertAllowedCodes(allFields, "provider:J611:02:107", "1");
        AssertAllowedCodes(allFields, "provider:J611:02:108", "1");
        allFields["provider:J611:02:009"].GetProperty("dataType").GetString().Should()
            .Be("code", "the official two-byte item is day-of-month, not an eight-byte calendar date");
        AssertAllowedCodes(allFields, "provider:J611:02:009",
            Enumerable.Range(1, 31).Select(day => day.ToString()).ToArray());
        failures.Should().BeEmpty(string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Field_mapping_is_a_total_function_over_csv_fields()
    {
        using var common = ReadEmbeddedJson("common-r7-10.json");
        using var provider = ReadEmbeddedJson("provider-claim-r7-10.json");
        using var mapping = ReadEmbeddedJson("field-mapping-r7-10.json");

        var csvFieldIds = common.RootElement.GetProperty("records").EnumerateArray()
            .Concat(provider.RootElement.GetProperty("records").EnumerateArray())
            .SelectMany(record => record.GetProperty("fields").EnumerateArray())
            .Select(field => field.GetProperty("fieldId").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        var fieldRequiredConditions = common.RootElement.GetProperty("records").EnumerateArray()
            .Concat(provider.RootElement.GetProperty("records").EnumerateArray())
            .SelectMany(record => record.GetProperty("fields").EnumerateArray())
            .ToDictionary(
                field => field.GetProperty("fieldId").GetString()!,
                field => field.GetProperty("requiredWhen").GetString()!,
                StringComparer.Ordinal);

        var mappings = mapping.RootElement.GetProperty("mappings").EnumerateArray().ToArray();
        var mappingIds = mappings.Select(item => item.GetProperty("fieldId").GetString()!).ToArray();
        var failures = new List<string>();
        failures.AddRange(csvFieldIds.Except(mappingIds).OrderBy(id => id).Select(id => $"missing mapping: {id}"));
        failures.AddRange(mappingIds.Except(csvFieldIds).OrderBy(id => id).Select(id => $"orphan mapping: {id}"));
        failures.AddRange(Duplicates(mappingIds).Select(id => $"duplicate mapping: {id}"));

        foreach (var item in mappings)
        {
            var fieldId = item.GetProperty("fieldId").GetString()!;
            var status = item.GetProperty("status").GetString();
            item.GetProperty("requiredCondition").GetString().Should()
                .Be(fieldRequiredConditions[fieldId], $"{fieldId} mapping condition drifted from its field specification");
            switch (status)
            {
                case "existing":
                    RequireNonBlank(item, fieldId, "modelPath", failures);
                    break;
                case "missing":
                    RequireNonBlank(item, fieldId, "targetModel", failures);
                    RequireNonBlank(item, fieldId, "targetProperty", failures);
                    RequireNonBlank(item, fieldId, "uiSurface", failures);
                    if (!item.TryGetProperty("migrationRequired", out var migrationRequired)
                        || migrationRequired.ValueKind != JsonValueKind.True)
                    {
                        failures.Add($"{fieldId}: missing mapping must set migrationRequired=true");
                    }
                    break;
                case "explicitInput":
                    RequireNonBlank(item, fieldId, "inputContract", failures);
                    break;
                case "generated":
                    RequireNonBlank(item, fieldId, "generatorRule", failures);
                    break;
                default:
                    failures.Add($"{fieldId}: forbidden status {status}");
                    break;
            }
        }

        mappings.Length.Should().Be(443);
        AssertMapping(mappings, "provider:J121:01:008", "existing", "Recipient.KanaName");
        AssertMapping(mappings, "provider:J121:01:011", "generated", "set 1");
        AssertMapping(mappings, "provider:J121:01:012", "existing", "Certificate.MonthlyCostCap");
        AssertMapping(mappings, "provider:J121:01:013", "generated", "set 1");
        AssertMapping(mappings, "provider:J121:01:015", "missing", "UpperLimitManagementProviderNumber");
        AssertMapping(mappings, "provider:J121:01:017", "missing", "UpperLimitManagedAmountYen");
        AssertMapping(mappings, "provider:J611:02:021", "existing", "DailyRecord.Transport");
        AssertMapping(mappings, "provider:J611:02:022", "existing", "DailyRecord.Transport");
        AssertMapping(mappings, "provider:J611:02:032", "existing", "DailyRecord.MealProvided");
        AssertMapping(mappings, "provider:J611:01:053", "generated", "OffsiteSupportApplied");
        AssertMapping(mappings, "provider:J611:01:054", "generated", "OffsiteSupportApplied");
        AssertMapping(mappings, "provider:J611:01:156", "missing", "StartDate");

        foreach (var existing in mappings.Where(item => item.GetProperty("status").GetString() == "existing"))
        {
            AssertModelPathExists(existing.GetProperty("modelPath").GetString()!);
        }

        failures.Should().BeEmpty(string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Source_catalog_has_reproducible_official_bytes()
    {
        using var sources = ReadEmbeddedJson("sources.json");
        var failures = new List<string>();

        foreach (var source in sources.RootElement.GetProperty("sources").EnumerateArray())
        {
            var id = source.GetProperty("sourceDocumentId").GetString()!;
            source.GetProperty("url").GetString().Should().StartWith("https://www.mhlw.go.jp/", id);
            source.GetProperty("version").GetString().Should().NotBeNullOrWhiteSpace(id);
            source.GetProperty("retrievedAt").GetString().Should().Be("2026-07-10", id);
            var sha256 = source.GetProperty("sha256").GetString();
            if (sha256 is null || sha256.Length != 64 || !sha256.All(Uri.IsHexDigit))
            {
                failures.Add($"{id}: sha256 is not 64 lowercase hexadecimal characters");
            }

            if (!source.TryGetProperty("sizeBytes", out var size) || size.GetInt64() <= 0)
            {
                failures.Add($"{id}: sizeBytes must be positive");
            }
        }

        failures.Should().BeEmpty(string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Specification_strings_do_not_contain_placeholders()
    {
        foreach (var file in new[]
                 {
                     "sources.json", "common-r7-10.json", "provider-claim-r7-10.json",
                     "field-mapping-r7-10.json",
                 })
        {
            using var document = ReadEmbeddedJson(file);
            var serialized = document.RootElement.GetRawText();
            foreach (var token in new[] { "<", ">", "TBD", "unknown", "assumed", "placeholder" })
            {
                serialized.Should().NotContain(token, $"{file} contains forbidden token {token}");
            }
        }
    }

    [Fact]
    public void Source_catalog_values_are_frozen_in_adr_0024()
    {
        using var sources = ReadEmbeddedJson("sources.json");
        var adrPath = Path.Combine(
            FindSolutionRoot(), "docs", "decisions", "0024-kokuhoren-csv-and-field-mapping.md");
        File.Exists(adrPath).Should().BeTrue($"missing ADR: {adrPath}");
        var adr = File.ReadAllText(adrPath);

        foreach (var source in sources.RootElement.GetProperty("sources").EnumerateArray())
        {
            foreach (var property in new[]
                     {
                         "sourceDocumentId", "version", "retrievedAt", "url", "sha256",
                     })
            {
                adr.Should().Contain(source.GetProperty(property).GetString()!,
                    $"ADR 0024 must freeze {property} for {source.GetProperty("sourceDocumentId").GetString()}");
            }
        }
    }

    private static JsonDocument ReadEmbeddedJson(string fileName)
    {
        var resourceName = CsvAssembly.GetManifestResourceNames()
            .SingleOrDefault(name => name.EndsWith($"Specifications.{fileName}", StringComparison.Ordinal));
        resourceName.Should().NotBeNull($"missing embedded specification: {fileName}");
        using var stream = CsvAssembly.GetManifestResourceStream(resourceName!);
        stream.Should().NotBeNull(resourceName);
        return JsonDocument.Parse(stream!);
    }

    private static string FindSolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Tsumugi.sln")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("Tsumugi.sln must be reachable from the test output directory");
        return directory!.FullName;
    }

    private static IEnumerable<string> Duplicates(IEnumerable<string> values) =>
        values.GroupBy(value => value, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);

    private static void AssertMapping(
        IEnumerable<JsonElement> mappings,
        string fieldId,
        string expectedStatus,
        string expectedValue)
    {
        var mapping = mappings.Single(item => item.GetProperty("fieldId").GetString() == fieldId);
        mapping.GetProperty("status").GetString().Should().Be(expectedStatus, fieldId);
        var value = expectedStatus switch
        {
            "existing" => mapping.GetProperty("modelPath").GetString(),
            "missing" => mapping.GetProperty("targetProperty").GetString(),
            "generated" => mapping.GetProperty("generatorRule").GetString(),
            _ => throw new InvalidOperationException(expectedStatus),
        };
        value.Should().Contain(expectedValue, fieldId);
    }

    private static void AssertAllowedCodes(
        Dictionary<string, JsonElement> fields,
        string fieldId,
        params string[] expected)
    {
        var actual = fields[fieldId].GetProperty("allowedCodes").EnumerateArray()
            .Select(code => code.GetString()).ToArray();
        actual.Should().Equal(expected, fieldId);
    }

    private static void AssertModelPathExists(string modelPath)
    {
        var parts = modelPath.Split('.', StringSplitOptions.RemoveEmptyEntries);
        parts.Should().HaveCount(2, modelPath);
        var type = typeof(Domain.Entities.Office).Assembly.GetType($"Tsumugi.Domain.Entities.{parts[0]}");
        type.Should().NotBeNull($"{modelPath} declares a real domain entity");
        type!.GetProperty(parts[1]).Should().NotBeNull($"{modelPath} declares a real domain property");
    }

    private static void RequireNonBlank(
        JsonElement item,
        string fieldId,
        string property,
        List<string> failures)
    {
        if (!item.TryGetProperty(property, out var value)
            || value.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(value.GetString()))
        {
            failures.Add($"{fieldId}: {property} is required");
        }
    }
}
