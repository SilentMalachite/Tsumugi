using System.Text;
using System.Text.Json;
using FluentAssertions;
using Tsumugi.Infrastructure.Csv.Specifications;

namespace Tsumugi.Infrastructure.Csv.Tests;

public sealed class CsvSpecificationLoaderTests
{
    [Fact]
    public void LoadEmbedded_returns_ordered_records_fields_and_total_mapping()
    {
        var catalog = CsvSpecificationLoader.LoadEmbedded();

        catalog.Version.Should().Be("r7-10");
        catalog.CommonRecords.Select(record => record.Order).Should().BeInAscendingOrder();
        catalog.ProviderRecords.Select(record => record.Order).Should().BeInAscendingOrder();
        catalog.CommonRecords.Concat(catalog.ProviderRecords).Should().OnlyContain(record =>
            record.Fields.Select(field => field.Position).SequenceEqual(
                Enumerable.Range(1, record.Fields.Count)));
        catalog.MappingByFieldId.Should().HaveCount(443);
        catalog.MappingByFieldId["provider:J121:04:015"].SourceFieldIds.Should()
            .Equal("provider:J121:04:014");
        catalog.SourcesById.Should().ContainKeys("common-r7-10", "provider-r7-10");
    }

    [Fact]
    public void Load_rejects_duplicate_record_id()
    {
        var common = SpecificationJson(records: $"{RecordJson("common:record", "common:field:001")},{RecordJson("common:record", "common:field:002", order: 2)}");

        var action = () => Load(common: common, mapping: MappingJson(
            MappingItemJson("common:field:001"),
            MappingItemJson("common:field:002")));

        action.Should().Throw<InvalidDataException>().WithMessage("*recordId*common:record*");
    }

    [Fact]
    public void Load_rejects_duplicate_field_id()
    {
        var common = SpecificationJson(records: $"{RecordJson("common:first", "common:field:001")},{RecordJson("common:second", "common:field:001", order: 2)}");

        var action = () => Load(common: common);

        action.Should().Throw<InvalidDataException>().WithMessage("*fieldId*common:field:001*");
    }

    [Fact]
    public void Load_rejects_duplicate_mapping_id()
    {
        var action = () => Load(mapping: MappingJson(
            MappingItemJson("common:field:001"),
            MappingItemJson("common:field:001")));

        action.Should().Throw<InvalidDataException>().WithMessage("*mapping fieldId*common:field:001*");
    }

    [Fact]
    public void Load_rejects_duplicate_source_id()
    {
        var action = () => Load(sources: SourcesJson(SourceJson(), SourceJson()));

        action.Should().Throw<InvalidDataException>().WithMessage("*sourceDocumentId*source:official*");
    }

    [Fact]
    public void Load_rejects_missing_field_position()
    {
        var common = SpecificationJson(records: RecordJson(
            "common:record", "common:field:001", fieldPosition: 2));

        var action = () => Load(common: common);

        action.Should().Throw<InvalidDataException>().WithMessage("*recordId*common:record*position*");
    }

    [Fact]
    public void Load_rejects_invalid_source_sha256()
    {
        var action = () => Load(sources: SourcesJson(SourceJson(sha256: "INVALID")));

        action.Should().Throw<InvalidDataException>().WithMessage("*sourceDocumentId*source:official*sha256*");
    }

    [Fact]
    public void Load_rejects_unknown_record_source()
    {
        var common = SpecificationJson(records: RecordJson(
            "common:record", "common:field:001", sourceDocumentId: "source:unknown"));

        var action = () => Load(common: common);

        action.Should().Throw<InvalidDataException>().WithMessage("*recordId*common:record*source:unknown*");
    }

    [Fact]
    public void Load_rejects_missing_mapping()
    {
        var action = () => Load(mapping: MappingJson());

        action.Should().Throw<InvalidDataException>().WithMessage("*fieldId*common:field:001*mapping*");
    }

    [Theory]
    [InlineData("", "always")]
    [InlineData("always", "")]
    public void Load_rejects_blank_required_condition(
        string requiredWhen,
        string requiredCondition)
    {
        var common = SpecificationJson(records: RecordJson(
            "common:record", "common:field:001", requiredWhen: requiredWhen));
        var mapping = MappingJson(MappingItemJson(
            "common:field:001", requiredCondition: requiredCondition));

        var action = () => Load(common: common, mapping: mapping);

        action.Should().Throw<InvalidDataException>().WithMessage("*fieldId*common:field:001*required*");
    }

    [Fact]
    public void Load_rejects_unknown_json_property()
    {
        var common = SpecificationJson(
            records: RecordJson("common:record", "common:field:001"),
            extraProperty: ",\"unexpected\":true");

        var action = () => Load(common: common);

        action.Should().Throw<JsonException>().WithMessage("*unexpected*");
    }

    [Fact]
    public void Load_rejects_unknown_live_check_property()
    {
        var sources = SourcesJson(SourceJson(
            extraProperty: ",\"liveCheck\":{\"unexpected\":true}"));

        var action = () => Load(sources: sources);

        action.Should().Throw<JsonException>().WithMessage("*unexpected*");
    }

    [Fact]
    public void Load_rejects_unknown_source_contract_property()
    {
        var mapping = MappingJson(MappingItemJson(
            "common:field:001",
            extraProperty: ",\"sourceContracts\":[{\"unexpected\":true}]"));

        var action = () => Load(mapping: mapping);

        action.Should().Throw<JsonException>().WithMessage("*unexpected*");
    }

    [Fact]
    public void Load_rejects_null_required_collection()
    {
        const string common = """
            {
              "schemaVersion": 1,
              "specificationVersion": "common-r7-10",
              "records": null
            }
            """;

        var action = () => Load(common: common);

        action.Should().Throw<JsonException>().WithMessage("*records*");
    }

    [Fact]
    public void Load_rejects_unknown_mapping_source_field()
    {
        var mapping = MappingJson(MappingItemJson(
            "common:field:001",
            extraProperty: ",\"sourceFieldIds\":[\"common:field:unknown\"]"));

        var action = () => Load(mapping: mapping);

        action.Should().Throw<InvalidDataException>()
            .WithMessage("*fieldId*common:field:001*common:field:unknown*");
    }

    private static CsvSpecificationCatalog Load(
        string? common = null,
        string? provider = null,
        string? mapping = null,
        string? sources = null)
    {
        using var commonStream = StreamOf(common ?? SpecificationJson(
            records: RecordJson("common:record", "common:field:001")));
        using var providerStream = StreamOf(provider ?? SpecificationJson(
            version: "provider-claim-r7-10", records: ""));
        using var mappingStream = StreamOf(mapping ?? MappingJson(
            MappingItemJson("common:field:001")));
        using var sourcesStream = StreamOf(sources ?? SourcesJson(SourceJson()));

        return CsvSpecificationLoader.Load(
            commonStream,
            providerStream,
            mappingStream,
            sourcesStream);
    }

    private static MemoryStream StreamOf(string json) =>
        new(Encoding.UTF8.GetBytes(json));

    private static string SpecificationJson(
        string records,
        string version = "common-r7-10",
        string extraProperty = "") =>
        $$"""
        {
          "schemaVersion": 1,
          "specificationVersion": "{{version}}"{{extraProperty}},
          "records": [{{records}}]
        }
        """;

    private static string RecordJson(
        string recordId,
        string fieldId,
        int order = 1,
        int fieldPosition = 1,
        string sourceDocumentId = "source:official",
        string requiredWhen = "always") =>
        $$"""
        {
          "recordId": "{{recordId}}",
          "exchangeInformationId": "J121",
          "innerRecordType": "01",
          "order": {{order}},
          "sourceDocumentId": "{{sourceDocumentId}}",
          "sourcePage": 1,
          "fields": [{
            "fieldId": "{{fieldId}}",
            "position": {{fieldPosition}},
            "officialName": "official field",
            "requiredWhen": "{{requiredWhen}}",
            "dataType": "text",
            "maxBytes": 10,
            "quoteRule": "quote",
            "allowedCodes": [],
            "sourcePage": 1,
            "requiredWhenSource": "source:official page 1"
          }]
        }
        """;

    private static string MappingJson(params string[] mappings) =>
        $$"""
        {
          "schemaVersion": 1,
          "specificationVersion": "field-mapping-r7-10",
          "mappings": [{{string.Join(',', mappings)}}]
        }
        """;

    private static string MappingItemJson(
        string fieldId,
        string requiredCondition = "always",
        string extraProperty = "") =>
        $$"""
        {
          "fieldId": "{{fieldId}}",
          "requiredCondition": "{{requiredCondition}}",
          "notes": "generated for test",
          "status": "generated",
          "generatorRule": "const(value=1)"{{extraProperty}}
        }
        """;

    private static string SourcesJson(params string[] sources) =>
        $$"""
        {
          "schemaVersion": 1,
          "sources": [{{string.Join(',', sources)}}]
        }
        """;

    private static string SourceJson(
        string sha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
        string extraProperty = "") =>
        $$"""
        {
          "sourceDocumentId": "source:official",
          "title": "official source",
          "version": "r7-10",
          "retrievedAt": "2026-07-10",
          "url": "https://www.mhlw.go.jp/example.pdf",
          "sha256": "{{sha256}}",
          "sizeBytes": 1{{extraProperty}}
        }
        """;
}
