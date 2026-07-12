using System.Text;
using System.Text.Json.Nodes;
using FluentAssertions;
using Tsumugi.Infrastructure.ClaimMasters;

namespace Tsumugi.Infrastructure.Tests.ClaimMasters;

public sealed class ClaimMasterSchemaPhase31Tests
{
    private const string Sha256 =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public void Load_accepts_a_complete_synthetic_calculation_master_bundle()
    {
        var action = () => Load(ValidMasters());

        action.Should().NotThrow();
    }

    [Fact]
    public void Load_rejects_a_row_without_source_locator()
    {
        var masters = ValidMasters();
        MutateFirstEntry(masters, "basic-rewards.json", entry =>
            entry.Remove("sourceLocator"));

        var action = () => Load(masters);

        action.Should().Throw<InvalidDataException>();
    }

    [Theory]
    [InlineData("percentageBaseScope", "unknown-scope")]
    [InlineData("percentageApplicationKind", "unknown-kind")]
    public void Load_rejects_unknown_closed_percentage_values(string propertyName, string value)
    {
        var masters = ValidMasters();
        MutateFirstValues(masters, "additions.json", values => values[propertyName] = value);

        var action = () => Load(masters);

        action.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Load_rejects_duplicate_calculation_order_for_the_same_selector()
    {
        var masters = ValidMasters();
        var root = JsonNode.Parse(masters["additions.json"])!.AsObject();
        var duplicate = root["entries"]![0]!.DeepClone().AsObject();
        duplicate["key"] = "addition-2";
        root["entries"]!.AsArray().Add(duplicate);
        masters["additions.json"] = root.ToJsonString();

        var action = () => Load(masters);

        action.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Load_rejects_a_double_derived_percentage_number()
    {
        var masters = ValidMasters();
        MutateFirstValues(masters, "additions.json", values => values["percentage"] = 0.1);

        var action = () => Load(masters);

        action.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Load_rejects_an_unknown_rounding_rule_id()
    {
        var masters = ValidMasters();
        MutateFirstValues(masters, "additions.json", values =>
            values["roundingRuleId"] = "claim.rounding.unknown.v1");

        var action = () => Load(masters);

        action.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Load_rejects_a_service_code_reference_outside_the_reward_period()
    {
        var masters = ValidMasters();
        MutateFirstEntry(masters, "service-codes.json", entry =>
            entry["effectiveFrom"] = "2027-01");

        var action = () => Load(masters);

        action.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Load_rejects_a_two_node_percentage_selector_cycle_in_the_same_period()
    {
        var masters = ValidMasters();
        var additions = JsonNode.Parse(masters["additions.json"])!.AsObject();
        var first = additions["entries"]![0]!.AsObject();
        first["key"] = "selector:a";
        first["values"]!["targetSelector"] = "selector:b";
        var second = first.DeepClone().AsObject();
        second["key"] = "selector:b";
        second["values"]!["targetSelector"] = "selector:a";
        additions["entries"]!.AsArray().Add(second);
        masters["additions.json"] = additions.ToJsonString();
        MutateFirstEntry(masters, "service-codes.json", entry =>
            entry["values"]!["selectors"] = new JsonArray("selector:a", "selector:b"));

        var action = () => Load(masters);

        action.Should().Throw<InvalidDataException>();
    }

    internal static JsonClaimMasterProvider CreateProvider(
        IReadOnlyDictionary<string, string> masterJsons) =>
        CreateProvider(masterJsons, ValidCatalogJson);

    internal static JsonClaimMasterProvider CreateProvider(
        IReadOnlyDictionary<string, string> masterJsons,
        string catalogJson)
    {
        using var sources = StreamOf(catalogJson);
        var streams = masterJsons.ToDictionary(
            pair => pair.Key,
            pair => (Stream)StreamOf(pair.Value),
            StringComparer.Ordinal);
        try
        {
            return JsonClaimMasterProvider.Load(sources, streams);
        }
        finally
        {
            foreach (var stream in streams.Values)
                stream.Dispose();
        }
    }

    private static void Load(IReadOnlyDictionary<string, string> masterJsons) =>
        _ = CreateProvider(masterJsons);

    internal static Dictionary<string, string> ValidMasters() =>
        new(StringComparer.Ordinal)
        {
            ["basic-rewards.json"] = MasterJson("basic-rewards", $$"""
                {
                  "key": "basic-1",
                  "effectiveFrom": "2026-06",
                  "effectiveTo": null,
                  "sourceDocumentId": "doc-1",
                  "sourceSha256": "{{Sha256}}",
                  "sourceLocator": "sheet:basic!A2:F2",
                  "values": {
                    "paymentBand": "band-1",
                    "staffingKey": "staffing-1",
                    "capacityKey": "capacity-1",
                    "serviceCode": "461001",
                    "units": 100
                  }
                }
                """),
            ["additions.json"] = MasterJson("additions", $$"""
                {
                  "key": "addition-1",
                  "effectiveFrom": "2026-06",
                  "effectiveTo": null,
                  "sourceDocumentId": "doc-1",
                  "sourceSha256": "{{Sha256}}",
                  "sourceLocator": "sheet:additions!A2:H2",
                  "values": {
                    "percentage": "0.10",
                    "percentageBaseScope": "per-service-code-unit",
                    "percentageApplicationKind": "add",
                    "targetSelector": "selector:basic",
                    "calculationOrder": 1,
                    "roundingRuleId": "claim.rounding.units.half-up.v1",
                    "calculationStepId": "claim.step.units.per-service-code.percentage.v1"
                  }
                }
                """),
            ["region-unit-prices.json"] = MasterJson("region-unit-prices", $$"""
                {
                  "key": "region-1",
                  "effectiveFrom": "2026-06",
                  "effectiveTo": null,
                  "sourceDocumentId": "doc-1",
                  "sourceSha256": "{{Sha256}}",
                  "sourceLocator": "html:pageNo=1#region-1",
                  "values": {
                    "regionKey": "region-1",
                    "serviceKind": "employment-continuation-support-b",
                    "unitPriceYen": "11.20"
                  }
                }
                """),
            ["burden-caps.json"] = MasterJson("burden-caps", $$"""
                {
                  "key": "burden-1",
                  "effectiveFrom": "2026-06",
                  "effectiveTo": null,
                  "sourceDocumentId": "doc-1",
                  "sourceSha256": "{{Sha256}}",
                  "sourceLocator": "pdf:physical-page=1#table=1,row=1",
                  "values": {
                    "burdenCategory": "category-1",
                    "capYen": 37200
                  }
                }
                """),
            ["transition-rules.json"] = MasterJson("transition-rules", $$"""
                {
                  "key": "office-profile-policy",
                  "effectiveFrom": "2026-06",
                  "effectiveTo": null,
                  "sourceDocumentId": "doc-1",
                  "sourceSha256": "{{Sha256}}",
                  "sourceLocator": "pdf:physical-page=2#section=transition",
                  "values": {
                    "masterVersion": "claim-master-test",
                    "allowedAverageWageBandOptions": [
                      { "kind": "numeric", "officialOptionCode": 1 },
                      { "kind": "filed-transition", "officialOptionCode": 8 }
                    ],
                    "allowedOptionsByR8ReformStatus": {
                      "reform-target": [
                        { "kind": "numeric", "officialOptionCode": 1 },
                        { "kind": "filed-transition", "officialOptionCode": 8 }
                      ]
                    },
                    "r8EffectiveDate": "2026-06-01",
                    "filedTransitionEndRule": "add-years-exclusive",
                    "filedTransitionDurationYears": 1
                  }
                }
                """),
            ["service-codes.json"] = MasterJson("service-codes", $$"""
                {
                  "key": "service-code-1",
                  "effectiveFrom": "2026-06",
                  "effectiveTo": null,
                  "sourceDocumentId": "doc-1",
                  "sourceSha256": "{{Sha256}}",
                  "sourceLocator": "sheet:codes!A2:D2",
                  "values": {
                    "serviceCode": "461001",
                    "serviceKind": "employment-continuation-support-b",
                    "selectors": ["selector:basic"]
                  }
                }
                """),
        };

    internal static void MutateFirstEntry(
        IDictionary<string, string> masters,
        string fileName,
        Action<JsonObject> mutate)
    {
        var root = JsonNode.Parse(masters[fileName])!.AsObject();
        mutate(root["entries"]![0]!.AsObject());
        masters[fileName] = root.ToJsonString();
    }

    private static void MutateFirstValues(
        IDictionary<string, string> masters,
        string fileName,
        Action<JsonObject> mutate) =>
        MutateFirstEntry(masters, fileName, entry => mutate(entry["values"]!.AsObject()));

    private static string MasterJson(string kind, string entry) => $$"""
        {
          "schemaVersion": "1",
          "masterKind": "{{kind}}",
          "entries": [{{entry}}]
        }
        """;

    private static MemoryStream StreamOf(string json) => new(Encoding.UTF8.GetBytes(json));

    private static string ValidCatalogJson => $$"""
        {
          "schemaVersion": "1",
          "sources": [
            {
              "documentId": "doc-1",
              "title": "Synthetic source",
              "publisher": "Test publisher",
              "effectiveAt": "2026-06-01",
              "publishedAt": "2026-05-01",
              "retrievedAt": "2026-06-02",
              "url": "https://example.test/doc-1.pdf",
              "sha256": "{{Sha256}}",
              "supersedes": null,
              "corrects": null,
              "supplements": null,
              "applicabilityNote": null,
              "correctionNote": null
            }
          ],
          "releases": [
            {
              "masterVersion": "claim-master-test",
              "effectiveFrom": "2026-06",
              "effectiveTo": null,
              "sourceDocumentIds": ["doc-1"]
            }
          ]
        }
        """;
}
