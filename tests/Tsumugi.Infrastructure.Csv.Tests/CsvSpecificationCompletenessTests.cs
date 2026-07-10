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
        mappings.GroupBy(item => item.GetProperty("status").GetString()!)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal)
            .Should().BeEquivalentTo(new Dictionary<string, int>
            {
                ["generated"] = 374,
                ["existing"] = 28,
                ["missing"] = 31,
                ["explicitInput"] = 10,
            });
        AssertMapping(mappings, "provider:J121:01:008", "existing", "Recipient.KanaName");
        AssertMapping(mappings, "provider:J121:01:011", "generated", "value=1");
        AssertMapping(mappings, "provider:J121:01:012", "existing", "Certificate.MonthlyCostCap");
        AssertMapping(mappings, "provider:J121:01:013", "generated", "value=1");
        AssertMapping(mappings, "provider:J121:01:015", "missing", "UpperLimitManagementProviderNumber");
        AssertMapping(mappings, "provider:J121:01:017", "missing", "UpperLimitManagedAmountYen");
        AssertMapping(mappings, "provider:J121:04:015", "missing", "MunicipalityDeterminedUserChargeYen");
        AssertMapping(mappings, "provider:J121:04:025", "missing", "MunicipalSubsidyAmountYen");
        AssertMapping(mappings, "provider:J121:04:030", "missing", "ExceptionalUsageStartMonth");
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

    [Fact]
    public void Csv_conditions_and_generator_rules_are_machine_decidable_and_field_specific()
    {
        using var common = ReadEmbeddedJson("common-r7-10.json");
        using var provider = ReadEmbeddedJson("provider-claim-r7-10.json");
        using var mapping = ReadEmbeddedJson("field-mapping-r7-10.json");

        var records = common.RootElement.GetProperty("records").EnumerateArray()
            .Concat(provider.RootElement.GetProperty("records").EnumerateArray())
            .ToArray();
        var fieldIds = records.SelectMany(record => record.GetProperty("fields").EnumerateArray())
            .Select(field => field.GetProperty("fieldId").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        var recordIds = records.Select(record => record.GetProperty("recordId").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        var failures = new List<string>();

        foreach (var record in records)
        {
            foreach (var field in record.GetProperty("fields").EnumerateArray())
            {
                var fieldId = field.GetProperty("fieldId").GetString()!;
                ValidateCondition(field.GetProperty("requiredWhen").GetString()!, fieldId, fieldIds, recordIds, failures);
                RequireNonBlank(field, fieldId, "requiredWhenSource", failures);
                if (!field.TryGetProperty("sourcePage", out var sourcePage) || sourcePage.GetInt32() <= 0)
                {
                    failures.Add($"{fieldId}: field-level sourcePage is required");
                }
            }
        }

        var generated = mapping.RootElement.GetProperty("mappings").EnumerateArray()
            .Where(item => item.GetProperty("status").GetString() == "generated")
            .ToArray();
        AssertDeterministicGeneratorRules(generated,
            fieldIds.Concat(recordIds).ToHashSet(StringComparer.Ordinal), failures);

        common.RootElement.GetProperty("records").EnumerateArray()
            .Should().OnlyContain(record => record.GetProperty("sourcePage").GetInt32() == 6,
                "all 19 outer CSV field definitions are on physical page 6; page 5 only describes framing");
        generated.Single(item => item.GetProperty("fieldId").GetString() == "provider:J111:01:006")
            .GetProperty("generatorRule").GetString().Should()
            .ContainAll("provider:J111:01:020", "provider:J111:01:021", "provider:J111:01:023", "sum(");
        generated.Single(item => item.GetProperty("fieldId").GetString() == "provider:J121:02:008")
            .GetProperty("generatorRule").GetString().Should()
            .ContainAll("min(", "DailyRecord.ServiceDate", "p23:B-type-setting");
        generated.Single(item => item.GetProperty("fieldId").GetString() == "provider:J121:04:012")
            .GetProperty("generatorRule").GetString().Should()
            .ContainAll("const(", "value=0", "serviceProvisionMonthFrom201204");
        generated.Single(item => item.GetProperty("fieldId").GetString() == "provider:J121:04:014")
            .GetProperty("generatorRule").GetString().Should().Contain("provider:J121:04:013/10");
        generated.Single(item => item.GetProperty("fieldId").GetString() == "provider:J121:04:016")
            .GetProperty("generatorRule").GetString().Should()
            .ContainAll("provider:J121:01:012", "provider:J121:04:015", "min(");
        failures.Should().BeEmpty(string.Join(Environment.NewLine, failures));
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

    internal static void ValidateCondition(
        string condition,
        string fieldId,
        IReadOnlySet<string> fieldIds,
        IReadOnlySet<string> recordIds,
        List<string> failures)
    {
        foreach (var phrase in new[]
                 {
                     "when the official", "when form", "must be empty", "condition applies",
                 })
        {
            if (condition.Contains(phrase, StringComparison.OrdinalIgnoreCase))
            {
                failures.Add($"{fieldId}: generic requiredWhen phrase: {condition}");
                return;
            }
        }

        if (condition is "always" or "optional" or "never")
        {
            return;
        }

        var open = condition.IndexOf('(');
        if (open <= 0 || !condition.EndsWith(')'))
        {
            failures.Add($"{fieldId}: unparseable condition: {condition}");
            return;
        }

        var operation = condition[..open];
        var arguments = SplitTopLevel(condition[(open + 1)..^1]);
        var unary = new HashSet<string>(StringComparer.Ordinal)
        {
            "recordPresent", "rowPresent", "modelPresent", "modelTrue", "modelNonZero",
            "inputPresent", "fieldPresent", "fieldNonZero", "serviceProvisionMonthBefore",
            "processingMonthBefore", "not",
        };
        if (operation is "all" or "any")
        {
            if (arguments.Count < 2)
            {
                failures.Add($"{fieldId}: {operation} requires at least two conditions");
            }
            foreach (var argument in arguments)
            {
                ValidateCondition(argument, fieldId, fieldIds, recordIds, failures);
            }
            return;
        }

        if (operation == "fieldEquals")
        {
            if (arguments.Count != 2 || !fieldIds.Contains(arguments[0]) || string.IsNullOrWhiteSpace(arguments[1]))
            {
                failures.Add($"{fieldId}: invalid fieldEquals condition: {condition}");
            }
            return;
        }

        if (!unary.Contains(operation) || arguments.Count != 1)
        {
            failures.Add($"{fieldId}: undefined condition operator: {condition}");
            return;
        }

        if (operation == "recordPresent" && !recordIds.Contains(arguments[0]))
        {
            failures.Add($"{fieldId}: unresolved record condition reference: {arguments[0]}");
        }
        else if ((operation is "fieldPresent" or "fieldNonZero") && !fieldIds.Contains(arguments[0]))
        {
            failures.Add($"{fieldId}: unresolved field condition reference: {arguments[0]}");
        }
        else if (operation == "not")
        {
            ValidateCondition(arguments[0], fieldId, fieldIds, recordIds, failures);
        }
        else if (operation is "serviceProvisionMonthBefore" or "processingMonthBefore"
                 && (!int.TryParse(arguments[0], out _) || arguments[0].Length != 6))
        {
            failures.Add($"{fieldId}: invalid month condition: {condition}");
        }
    }

    internal static void AssertDeterministicGeneratorRules(
        IReadOnlyCollection<JsonElement> generated,
        IReadOnlySet<string> allFieldIds,
        List<string> failures)
    {
        var rules = new List<string>();
        var allowedOperations = new HashSet<string>(StringComparer.Ordinal)
        {
            "aggregate", "calendarDay", "conditional", "const", "constEmpty", "copy", "count",
            "difference", "format", "lookup", "max", "min", "multiply", "payload", "recordCount",
            "render", "roundDown", "sequence", "sum",
        };
        foreach (var item in generated)
        {
            var fieldId = item.GetProperty("fieldId").GetString()!;
            var rule = item.GetProperty("generatorRule").GetString()!;
            rules.Add(rule);
            foreach (var phrase in new[]
                     {
                         "derive the value", "derive the official", "derive the outer", "render the value",
                         "effective contracts, daily records", "claim context", "selected claim context",
                     })
            {
                if (rule.Contains(phrase, StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add($"{fieldId}: generic generatorRule: {rule}");
                }
            }

            var open = rule.IndexOf('(');
            var operation = open > 0 && rule.EndsWith(')') ? rule[..open] : string.Empty;
            if (!allowedOperations.Contains(operation))
            {
                failures.Add($"{fieldId}: undefined generator operation: {rule}");
            }
            if (!rule.Contains($"target={fieldId}", StringComparison.Ordinal))
            {
                failures.Add($"{fieldId}: generatorRule must identify its target");
            }
            if (!rule.Contains("source=", StringComparison.Ordinal))
            {
                failures.Add($"{fieldId}: generatorRule must identify its official source");
            }

            foreach (System.Text.RegularExpressions.Match match in
                     System.Text.RegularExpressions.Regex.Matches(rule, @"(?:common|provider|report):[A-Za-z0-9:-]+"))
            {
                var reference = match.Value;
                if (reference != fieldId && !allFieldIds.Contains(reference))
                {
                    failures.Add($"{fieldId}: unresolved formula field reference: {reference}");
                }
            }
        }

        foreach (var duplicate in Duplicates(rules))
        {
            failures.Add($"generatorRule is reused instead of field-specific: {duplicate}");
        }
    }

    private static List<string> SplitTopLevel(string input)
    {
        var result = new List<string>();
        var depth = 0;
        var start = 0;
        for (var index = 0; index < input.Length; index++)
        {
            depth += input[index] == '(' ? 1 : input[index] == ')' ? -1 : 0;
            if (input[index] == ';' && depth == 0)
            {
                result.Add(input[start..index]);
                start = index + 1;
            }
        }
        result.Add(input[start..]);
        return result;
    }
}
