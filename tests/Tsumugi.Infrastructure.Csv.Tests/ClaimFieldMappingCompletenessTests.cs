using System.Text.Json;
using FluentAssertions;
using Tsumugi.Infrastructure.Csv;

namespace Tsumugi.Infrastructure.Csv.Tests;

public sealed class ClaimFieldMappingCompletenessTests
{
    private static readonly Dictionary<string, int> ExpectedArtifactFieldCounts =
        new Dictionary<string, int>
        {
            ["service-performance"] = 41,
            ["benefit-claim-form"] = 24,
            ["benefit-claim-detail"] = 48,
        };

    [Fact]
    public void Report_inventory_and_mapping_are_complete_and_bijective()
    {
        using var inventory = ReadSpecData("report-fields-r8-06.json");
        using var mapping = ReadSpecData("report-field-mapping-r8-06.json");
        using var csvMapping = ReadEmbeddedJson("field-mapping-r7-10.json");
        using var sources = ReadEmbeddedJson("sources.json");

        var fields = inventory.RootElement.GetProperty("fields").EnumerateArray().ToArray();
        var mappings = mapping.RootElement.GetProperty("mappings").EnumerateArray().ToArray();
        var fieldIds = fields.Select(field => field.GetProperty("fieldId").GetString()!).ToArray();
        var mappingIds = mappings.Select(item => item.GetProperty("fieldId").GetString()!).ToArray();
        var csvFieldIds = csvMapping.RootElement.GetProperty("mappings").EnumerateArray()
            .Select(item => item.GetProperty("fieldId").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        var sourceIds = sources.RootElement.GetProperty("sources").EnumerateArray()
            .Select(source => source.GetProperty("sourceDocumentId").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        var requiredConditions = fields.ToDictionary(
            field => field.GetProperty("fieldId").GetString()!,
            field => field.GetProperty("requiredWhen").GetString()!,
            StringComparer.Ordinal);

        var failures = new List<string>();
        failures.AddRange(Duplicates(fieldIds).Select(id => $"duplicate report fieldId: {id}"));
        failures.AddRange(Duplicates(mappingIds).Select(id => $"duplicate report mapping: {id}"));
        failures.AddRange(fieldIds.Except(mappingIds).OrderBy(id => id).Select(id => $"missing report mapping: {id}"));
        failures.AddRange(mappingIds.Except(fieldIds).OrderBy(id => id).Select(id => $"orphan report mapping: {id}"));

        foreach (var expected in ExpectedArtifactFieldCounts)
        {
            var actual = fields.Count(field => field.GetProperty("artifactId").GetString() == expected.Key);
            if (actual != expected.Value)
            {
                failures.Add($"{expected.Key}: expected {expected.Value} fields but found {actual}");
            }
        }

        foreach (var artifact in fields.GroupBy(field => field.GetProperty("artifactId").GetString()!))
        {
            foreach (var section in artifact.GroupBy(field => field.GetProperty("section").GetString()!))
            {
                var positions = section.Select(field => field.GetProperty("position").GetInt32()).ToArray();
                if (positions.Distinct().Count() != positions.Length)
                {
                    failures.Add($"{artifact.Key}/{section.Key}: duplicate position");
                }
            }
        }

        foreach (var field in fields)
        {
            var fieldId = field.GetProperty("fieldId").GetString()!;
            foreach (var property in new[] { "artifactId", "section", "officialName", "requiredWhen", "sourceDocumentId" })
            {
                if (string.IsNullOrWhiteSpace(field.GetProperty(property).GetString()))
                {
                    failures.Add($"{fieldId}: {property} is blank");
                }
            }

            if (field.GetProperty("sourcePage").GetInt32() <= 0)
            {
                failures.Add($"{fieldId}: sourcePage must be positive");
            }

            var sourceDocumentId = field.GetProperty("sourceDocumentId").GetString()!;
            if (!sourceIds.Contains(sourceDocumentId))
            {
                failures.Add($"{fieldId}: unresolved sourceDocumentId {sourceDocumentId}");
            }
        }

        foreach (var item in mappings)
        {
            var fieldId = item.GetProperty("fieldId").GetString()!;
            var status = item.GetProperty("status").GetString();
            item.GetProperty("requiredCondition").GetString().Should().Be(requiredConditions[fieldId],
                $"{fieldId} report mapping condition drifted from the independent report inventory");
            var requiredProperty = status switch
            {
                "existing" => "modelPath",
                "explicitInput" => "inputContract",
                "generated" => "generatorRule",
                "missing" => "targetProperty",
                _ => string.Empty,
            };
            if (requiredProperty.Length == 0)
            {
                failures.Add($"{fieldId}: forbidden status {status}");
            }
            else if (!item.TryGetProperty(requiredProperty, out var required)
                     || required.ValueKind != JsonValueKind.String
                     || string.IsNullOrWhiteSpace(required.GetString()))
            {
                failures.Add($"{fieldId}: {requiredProperty} is required for {status}");
            }

            if (status == "missing")
            {
                foreach (var property in new[] { "targetModel", "uiSurface" })
                {
                    if (!item.TryGetProperty(property, out var value)
                        || value.ValueKind != JsonValueKind.String
                        || string.IsNullOrWhiteSpace(value.GetString()))
                    {
                        failures.Add($"{fieldId}: {property} is required for missing");
                    }
                }

                if (!item.TryGetProperty("migrationRequired", out var migrationRequired)
                    || migrationRequired.ValueKind != JsonValueKind.True)
                {
                    failures.Add($"{fieldId}: migrationRequired=true is required for missing");
                }
            }

            if (item.TryGetProperty("sameMeaningAsCsvFieldId", out var sameMeaning)
                && sameMeaning.ValueKind == JsonValueKind.String
                && !csvFieldIds.Contains(sameMeaning.GetString()!))
            {
                failures.Add($"{fieldId}: unresolved sameMeaningAsCsvFieldId {sameMeaning.GetString()}");
            }

            if (status == "existing")
            {
                AssertModelPathExists(item.GetProperty("modelPath").GetString()!);
            }
        }

        fields.Length.Should().Be(113);
        mappings.Length.Should().Be(113);
        mappings.GroupBy(item => item.GetProperty("status").GetString()!)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal)
            .Should().BeEquivalentTo(new Dictionary<string, int>
            {
                ["generated"] = 66,
                ["existing"] = 21,
                ["missing"] = 22,
                ["explicitInput"] = 4,
            });
        mappings.Count(item => item.TryGetProperty("sameMeaningAsCsvFieldId", out _)).Should().Be(89);
        AssertMapping(mappings, "report:service-performance:daily:006", "existing", "DailyRecord.Transport");
        AssertMapping(mappings, "report:service-performance:daily:007", "existing", "DailyRecord.Transport");
        AssertMapping(mappings, "report:service-performance:daily:009", "existing", "DailyRecord.MealProvided");
        AssertMapping(mappings, "report:service-performance:intensive-support:001", "missing", "StartDate");
        AssertMapping(mappings, "report:benefit-claim-form:header:002", "existing", "Certificate.Municipality");
        AssertMapping(mappings, "report:benefit-claim-form:header:004", "missing", "PostalCode");
        AssertMapping(mappings, "report:benefit-claim-form:header:005", "missing", "Address");
        AssertMapping(mappings, "report:benefit-claim-form:header:006", "missing", "PhoneNumber");
        AssertMapping(mappings, "report:benefit-claim-form:header:008", "missing", "RepresentativeTitleAndName");
        AssertMapping(mappings, "report:benefit-claim-detail:upper-limit-management:001", "missing", "UpperLimitManagementProviderNumber");
        AssertMapping(mappings, "report:benefit-claim-detail:upper-limit-management:002", "existing", "Certificate.UpperLimitManagementProvider");
        AssertMapping(mappings, "report:benefit-claim-detail:summary:007", "missing", "MunicipalityDeterminedUserChargeYen");
        AssertMapping(mappings, "report:benefit-claim-detail:summary:015", "missing", "MunicipalSubsidyAmountYen");
        failures.Should().BeEmpty(string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Human_mapping_document_lists_every_machine_readable_field()
    {
        using var csvMapping = ReadEmbeddedJson("field-mapping-r7-10.json");
        using var reportMapping = ReadSpecData("report-field-mapping-r8-06.json");
        var documentPath = Path.Combine(FindSolutionRoot(), "docs", "phase3-claim-field-mapping.md");
        File.Exists(documentPath).Should().BeTrue($"missing human mapping document: {documentPath}");
        var document = File.ReadAllText(documentPath);

        var fieldIds = csvMapping.RootElement.GetProperty("mappings").EnumerateArray()
            .Concat(reportMapping.RootElement.GetProperty("mappings").EnumerateArray())
            .Select(item => item.GetProperty("fieldId").GetString()!);

        var expected = fieldIds.ToHashSet(StringComparer.Ordinal);
        var documented = System.Text.RegularExpressions.Regex.Matches(
                document, @"^\|\s*((?:common|provider|report):[^| ]+)\s*\|",
                System.Text.RegularExpressions.RegexOptions.Multiline)
            .Select(match => match.Groups[1].Value)
            .ToArray();
        var missing = expected.Except(documented).ToArray();
        missing.Should().BeEmpty($"human mapping document omitted: {string.Join(", ", missing)}");
        documented.Should().HaveCount(556).And.OnlyHaveUniqueItems(
            "the human table must contain exactly one first-column row for every CSV and report field");
    }

    [Fact]
    public void Report_conditions_and_generator_rules_are_machine_decidable_and_field_specific()
    {
        using var inventory = ReadSpecData("report-fields-r8-06.json");
        using var mapping = ReadSpecData("report-field-mapping-r8-06.json");
        using var common = ReadEmbeddedJson("common-r7-10.json");
        using var provider = ReadEmbeddedJson("provider-claim-r7-10.json");

        var csvRecords = common.RootElement.GetProperty("records").EnumerateArray()
            .Concat(provider.RootElement.GetProperty("records").EnumerateArray())
            .ToArray();
        var reportFields = inventory.RootElement.GetProperty("fields").EnumerateArray().ToArray();
        var fieldIds = csvRecords.SelectMany(record => record.GetProperty("fields").EnumerateArray())
            .Select(field => field.GetProperty("fieldId").GetString()!)
            .Concat(reportFields.Select(field => field.GetProperty("fieldId").GetString()!))
            .ToHashSet(StringComparer.Ordinal);
        var recordIds = csvRecords.Select(record => record.GetProperty("recordId").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        var failures = new List<string>();

        foreach (var field in reportFields)
        {
            var fieldId = field.GetProperty("fieldId").GetString()!;
            CsvSpecificationCompletenessTests.ValidateCondition(
                field.GetProperty("requiredWhen").GetString()!, fieldId, fieldIds, recordIds, failures);
            foreach (var property in new[] { "requiredWhenSource" })
            {
                if (!field.TryGetProperty(property, out var value)
                    || value.ValueKind != JsonValueKind.String
                    || string.IsNullOrWhiteSpace(value.GetString()))
                {
                    failures.Add($"{fieldId}: {property} is required");
                }
            }
        }

        reportFields.Where(field => field.GetProperty("artifactId").GetString() == "service-performance")
            .Should().OnlyContain(field => field.GetProperty("sourcePage").GetInt32() == 12);
        reportFields.Where(field => field.GetProperty("artifactId").GetString() == "benefit-claim-form")
            .Should().OnlyContain(field => field.GetProperty("sourcePage").GetInt32() == 1);
        reportFields.Where(field => field.GetProperty("artifactId").GetString() == "benefit-claim-detail")
            .Should().OnlyContain(field => field.GetProperty("sourcePage").GetInt32() == 2);

        var generated = mapping.RootElement.GetProperty("mappings").EnumerateArray()
            .Where(item => item.GetProperty("status").GetString() == "generated")
            .ToArray();
        CsvSpecificationCompletenessTests.AssertDeterministicGeneratorRules(generated,
            fieldIds.Concat(recordIds).ToHashSet(StringComparer.Ordinal), failures);
        failures.Should().BeEmpty(string.Join(Environment.NewLine, failures));
    }

    private static JsonDocument ReadSpecData(string fileName)
    {
        var path = Path.Combine(FindSolutionRoot(), "docs", "spec-data", "phase3", fileName);
        File.Exists(path).Should().BeTrue($"missing report specification: {path}");
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static JsonDocument ReadEmbeddedJson(string fileName)
    {
        var assembly = typeof(CsvAssemblyMarker).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .SingleOrDefault(name => name.EndsWith($"Specifications.{fileName}", StringComparison.Ordinal));
        resourceName.Should().NotBeNull($"missing embedded specification: {fileName}");
        using var stream = assembly.GetManifestResourceStream(resourceName!);
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
        var value = expectedStatus == "existing"
            ? mapping.GetProperty("modelPath").GetString()
            : mapping.GetProperty("targetProperty").GetString();
        value.Should().Be(expectedValue, fieldId);
    }

    private static void AssertModelPathExists(string modelPath)
    {
        var parts = modelPath.Split('.', StringSplitOptions.RemoveEmptyEntries);
        parts.Should().HaveCount(2, modelPath);
        var type = typeof(Domain.Entities.Office).Assembly.GetType($"Tsumugi.Domain.Entities.{parts[0]}");
        type.Should().NotBeNull($"{modelPath} declares a real domain entity");
        type!.GetProperty(parts[1]).Should().NotBeNull($"{modelPath} declares a real domain property");
    }
}
