using System.Text;
using System.Text.Json;
using FluentAssertions;
using Tsumugi.Infrastructure.Csv.Specifications;

namespace Tsumugi.Infrastructure.Csv.Tests;

public sealed class CsvSpecificationLoaderTests
{
    public static TheoryData<string, string> ConflictingMappingSources => new()
    {
        {
            "generated",
            "\"generatorRule\":\"const(value=1)\",\"modelPath\":\"Office.OfficeNumber\""
        },
        {
            "existing",
            "\"modelPath\":\"Office.OfficeNumber\",\"generatorRule\":\"const(value=1)\""
        },
        {
            "explicitInput",
            "\"inputContract\":\"ProcessingMonth\",\"migrationRequired\":true"
        },
        {
            "missing",
            "\"migrationRequired\":true,\"targetModel\":\"Certificate\",\"targetProperty\":\"MunicipalityNumber\",\"uiSurface\":\"CertificateView\",\"inputContract\":\"ProcessingMonth\""
        },
        {
            "existing",
            "\"modelPath\":\"Office.OfficeNumber\",\"sourceFieldIds\":[\"common:field:001\"]"
        },
        {
            "existing",
            "\"modelPath\":\"Office.OfficeNumber\",\"sourceContracts\":[{\"contractId\":\"Claim.Lines\",\"itemType\":\"Line\",\"dateProperty\":\"Date\",\"lineKindProperty\":\"Kind\",\"includedLineKinds\":[\"Main\"],\"aggregation\":\"countDistinctDate\"}]"
        },
        {
            "generated",
            "\"generatorRule\":\"const(value=1)\",\"migrationRequired\":true,\"targetModel\":\"Certificate\",\"targetProperty\":\"MunicipalityNumber\",\"uiSurface\":\"CertificateView\""
        },
        {
            "generated",
            "\"generatorRule\":\"const(value=1)\",\"modelPath\":\"\""
        },
    };

    public static TheoryData<string> InvalidLiveChecks => new()
    {
        "{}",
        "{\"checkedAt\":\"2026-07-10\",\"httpStatus\":404,\"responseSha256\":\"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef\"}",
        "{\"checkedAt\":1,\"httpStatus\":\"404\",\"responseSha256\":true,\"responseSizeBytes\":\"1\"}",
        "{\"checkedAt\":\"\",\"httpStatus\":404,\"responseSha256\":\"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef\",\"responseSizeBytes\":1}",
        "{\"checkedAt\":\"2026-07-10\",\"httpStatus\":404,\"responseSha256\":\"ABCDEF\",\"responseSizeBytes\":1}",
        "{\"checkedAt\":\"2026-07-10\",\"httpStatus\":404,\"responseSha256\":\"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef\",\"responseSizeBytes\":0}",
    };

    public static TheoryData<string> InvalidSourceContracts => new()
    {
        "[]",
        "[{}]",
        "[{\"contractId\":\"Claim.Lines\",\"itemType\":\"Line\",\"dateProperty\":\"Date\",\"lineKindProperty\":\"Kind\",\"includedLineKinds\":[\"Main\"]}]",
        "[{\"contractId\":1,\"itemType\":\"Line\",\"dateProperty\":\"Date\",\"lineKindProperty\":\"Kind\",\"includedLineKinds\":\"Main\",\"aggregation\":true}]",
        "[{\"contractId\":\"Claim.Lines\",\"itemType\":\"Line\",\"dateProperty\":\"Date\",\"lineKindProperty\":\"Kind\",\"includedLineKinds\":[],\"aggregation\":\"countDistinctDate\"}]",
        "[{\"contractId\":\"Claim.Lines\",\"itemType\":\"Line\",\"dateProperty\":\"Date\",\"lineKindProperty\":\"Kind\",\"includedLineKinds\":[\"Main\"],\"aggregation\":\"countDistinctDate\",\"window\":\"\"}]",
    };

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

    [Fact]
    public void Load_rejects_duplicate_top_level_property()
    {
        var common = SpecificationJson(
            records: RecordJson("common:record", "common:field:001"))
            .Replace(
                "\"schemaVersion\": 1,",
                "\"schemaVersion\": 1, \"schemaVersion\": 1,",
                StringComparison.Ordinal);

        var action = () => Load(common: common);

        action.Should().Throw<JsonException>().WithMessage("*schemaVersion*");
    }

    [Fact]
    public void Load_rejects_duplicate_nested_property()
    {
        var record = RecordJson("common:record", "common:field:001")
            .Replace(
                "\"position\": 1,",
                "\"position\": 1, \"position\": 1,",
                StringComparison.Ordinal);
        var common = SpecificationJson(records: record);

        var action = () => Load(common: common);

        action.Should().Throw<JsonException>().WithMessage("*position*");
    }

    [Fact]
    public void Catalog_does_not_change_when_caller_collections_are_mutated()
    {
        var fixture = CreateMutableCatalog();
        using var sourceContractDocument = fixture.SourceContractDocument;

        fixture.AllowedCodes.Add("2");
        fixture.SourceFieldIds.Clear();
        fixture.SourceContracts.Clear();
        fixture.SourceSheets.Add("changed");
        fixture.ApplicablePages.Clear();
        fixture.ApplicablePageTextSha256["1"] = new string('f', 64);

        var field = fixture.Catalog.CommonRecords.Single().Fields.Single();
        var mapping = fixture.Catalog.MappingByFieldId["common:field:001"];
        var source = fixture.Catalog.SourcesById["source:official"];
        field.AllowedCodes.Should().Equal("1");
        mapping.SourceFieldIds.Should().Equal("common:field:001");
        mapping.SourceContracts.Should().ContainSingle();
        source.SourceSheets.Should().Equal("official sheet");
        source.ApplicablePages.Should().Equal(1);
        source.ApplicablePageTextSha256.Should().Contain("1", new string('0', 64));
    }

    [Fact]
    public void Catalog_nested_collections_are_read_only()
    {
        var fixture = CreateMutableCatalog();
        using var sourceContractDocument = fixture.SourceContractDocument;
        var field = fixture.Catalog.CommonRecords.Single().Fields.Single();
        var mapping = fixture.Catalog.MappingByFieldId["common:field:001"];
        var source = fixture.Catalog.SourcesById["source:official"];

        var mutations = new Action[]
        {
            () => ((IList<string>)field.AllowedCodes).Add("2"),
            () => ((IList<string>)mapping.SourceFieldIds!).Add("common:field:002"),
            () => ((IList<JsonElement>)mapping.SourceContracts!).Add(default),
            () => ((IList<string>)source.SourceSheets!).Add("changed"),
            () => ((IList<int>)source.ApplicablePages!).Add(2),
            () => ((IDictionary<string, string>)source.ApplicablePageTextSha256!)["1"] = new string('f', 64),
        };

        foreach (var mutation in mutations)
        {
            mutation.Should().Throw<NotSupportedException>();
        }
    }

    [Fact]
    public void Catalog_clones_source_contract_json_elements()
    {
        var fixture = CreateMutableCatalog();

        fixture.SourceContractDocument.Dispose();

        fixture.Catalog.MappingByFieldId["common:field:001"].SourceContracts![0]
            .GetProperty("contractId").GetString().Should().Be("Claim.Lines");
        fixture.Catalog.SourcesById["source:official"].LiveCheck!.Value
            .GetProperty("httpStatus").GetInt32().Should().Be(404);
    }

    [Theory]
    [MemberData(nameof(ConflictingMappingSources))]
    public void Load_rejects_conflicting_mapping_source_properties(
        string status,
        string sourceProperties)
    {
        var mapping = MappingJson(MappingItemWithSourcesJson(
            "common:field:001", status, sourceProperties));

        var action = () => Load(mapping: mapping);

        action.Should().Throw<InvalidDataException>()
            .WithMessage("*fieldId*common:field:001*status*");
    }

    [Theory]
    [MemberData(nameof(InvalidLiveChecks))]
    public void Load_rejects_invalid_live_check_shape(string liveCheck)
    {
        var sources = SourcesJson(SourceJson(
            extraProperty: $",\"liveCheck\":{liveCheck}"));

        var action = () => Load(sources: sources);

        action.Should().Throw<JsonException>().WithMessage("*source:official*liveCheck*");
    }

    [Theory]
    [MemberData(nameof(InvalidSourceContracts))]
    public void Load_rejects_invalid_source_contract_shape(string sourceContracts)
    {
        var mapping = MappingJson(MappingItemJson(
            "common:field:001",
            extraProperty: $",\"sourceContracts\":{sourceContracts}"));

        var action = () => Load(mapping: mapping);

        action.Should().Throw<JsonException>().WithMessage("*common:field:001*sourceContract*");
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

    private static string MappingItemWithSourcesJson(
        string fieldId,
        string status,
        string sourceProperties) =>
        $$"""
        {
          "fieldId": "{{fieldId}}",
          "requiredCondition": "always",
          "notes": "generated for test",
          "status": "{{status}}",
          {{sourceProperties}}
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

    private static MutableCatalogFixture CreateMutableCatalog()
    {
        var allowedCodes = new List<string> { "1" };
        var sourceFieldIds = new List<string> { "common:field:001" };
        var sourceContractDocument = JsonDocument.Parse("""
            {
              "sourceContract": {
                "contractId": "Claim.Lines",
                "itemType": "Line",
                "dateProperty": "Date",
                "lineKindProperty": "Kind",
                "includedLineKinds": ["Main"],
                "aggregation": "countDistinctDate"
              },
              "liveCheck": {
                "checkedAt": "2026-07-10",
                "httpStatus": 404,
                "responseSha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                "responseSizeBytes": 1
              }
            }
            """);
        var sourceContracts = new List<JsonElement>
        {
            sourceContractDocument.RootElement.GetProperty("sourceContract"),
        };
        var sourceSheets = new List<string> { "official sheet" };
        var applicablePages = new List<int> { 1 };
        var applicablePageTextSha256 = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["1"] = new string('0', 64),
        };
        var field = new CsvFieldSpecification(
            "common:field:001",
            1,
            "official field",
            "always",
            "code",
            1,
            "quote",
            allowedCodes,
            1,
            "source page 1");
        var record = new CsvRecordSpecification(
            "common:record",
            "J121",
            "01",
            1,
            "source:official",
            1,
            new List<CsvFieldSpecification> { field });
        var mapping = new CsvFieldMapping(
            "common:field:001",
            "always",
            "test",
            "generated",
            GeneratorRule: "const(value=1)",
            SourceContracts: sourceContracts,
            SourceFieldIds: sourceFieldIds);
        var source = new CsvSourceDocument(
            "source:official",
            "official source",
            "r7-10",
            "2026-07-10",
            "https://www.mhlw.go.jp/example.pdf",
            new string('0', 64),
            1,
            SourceSheets: sourceSheets,
            ApplicablePages: applicablePages,
            ApplicablePageTextSha256: applicablePageTextSha256,
            LiveCheck: sourceContractDocument.RootElement.GetProperty("liveCheck"));
        var catalog = new CsvSpecificationCatalog(
            "r7-10",
            new List<CsvRecordSpecification> { record },
            new List<CsvRecordSpecification>(),
            new Dictionary<string, CsvFieldMapping>(StringComparer.Ordinal)
            {
                [mapping.FieldId] = mapping,
            },
            new Dictionary<string, CsvSourceDocument>(StringComparer.Ordinal)
            {
                [source.SourceDocumentId] = source,
            });

        return new MutableCatalogFixture(
            catalog,
            allowedCodes,
            sourceFieldIds,
            sourceContracts,
            sourceSheets,
            applicablePages,
            applicablePageTextSha256,
            sourceContractDocument);
    }

    private sealed record MutableCatalogFixture(
        CsvSpecificationCatalog Catalog,
        List<string> AllowedCodes,
        List<string> SourceFieldIds,
        List<JsonElement> SourceContracts,
        List<string> SourceSheets,
        List<int> ApplicablePages,
        Dictionary<string, string> ApplicablePageTextSha256,
        JsonDocument SourceContractDocument);
}
