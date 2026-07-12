using System.Text.Json;
using FluentAssertions;
using Tsumugi.Infrastructure.Tests;

namespace Tsumugi.Infrastructure.Tests.ClaimMasters;

public sealed class ClaimMasterSeedPhase31Tests
{
    private const string ManifestPath =
        "docs/spec-data/phase3/claim-master-source-row-manifest.json";

    private static readonly string[] ExpectedDocumentIds =
    [
        "mhlw-unit-price-notice-observed-946c3d96",
        "r6-disability-support-guide-202404",
        "r6-capability-202404",
        "r6-capability-202406",
        "r6-service-codes-2-xlsx",
        "r8-capability-202606",
        "r8-service-codes-2-xlsx",
        "r6-revision-overview",
        "r6-fee-notice",
        "r6-calculation-note",
        "r6-employment-guidance-r6",
        "r6-employment-guidance",
        "r6-employment-guidance-corr-4",
        "r6-qa-v1",
        "r6-qa-v2",
        "r6-qa-v3",
        "r6-qa-v5",
        "r6-qa-v6",
        "r6-qa-v7",
        "r6-qa-v8",
        "r6-calculation-corr-1",
        "r6-calculation-corr-3",
        "r6-calculation-corr-5",
        "r6-calculation-corr-6",
        "r6-calculation-corr-7",
        "r6-calculation-corr-8",
        "r6-qa-corr-1",
        "r6-qa-corr-2",
        "r6-qa-corr-3",
        "r6-reward-structure",
        "r6-service-codes-2-pdf",
        "r8-revision-overview",
        "r8-fee-notice",
        "r8-calculation-note",
        "r8-b-reward-band-guide",
        "r8-employment-transition-guide",
        "r8-qa-v1",
        "r8-amendment-qa",
        "r8-capability-correction",
        "r8-reward-structure",
        "r8-service-codes-2-pdf",
    ];

    private static readonly string[] ExpectedAuthoritativeDocumentIds =
    [
        "mhlw-unit-price-notice-observed-946c3d96",
        "r6-disability-support-guide-202404",
        "r6-capability-202404",
        "r6-capability-202406",
        "r6-service-codes-2-xlsx",
        "r8-capability-202606",
        "r8-service-codes-2-xlsx",
    ];

    [Fact]
    public void Source_manifest_exists_and_has_a_closed_root_contract()
    {
        using var manifest = OpenRepositoryJson(ManifestPath);
        var root = manifest.RootElement;

        root.EnumerateObject().Select(property => property.Name)
            .Should().Equal("schemaVersion", "documents", "rows");
        root.GetProperty("schemaVersion").GetString().Should().Be("1");
        root.GetProperty("documents").ValueKind.Should().Be(JsonValueKind.Array);
        var rows = root.GetProperty("rows");
        rows.ValueKind.Should().Be(JsonValueKind.Array);
        rows.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public void Source_manifest_documents_match_the_catalog_and_release_bundles()
    {
        using var manifest = OpenRepositoryJson(ManifestPath);
        using var catalog = OpenRepositoryJson(
            "src/Tsumugi.Infrastructure/ClaimMasters/Seed/sources.json");

        var catalogSources = catalog.RootElement.GetProperty("sources")
            .EnumerateArray()
            .ToDictionary(
                source => source.GetProperty("documentId").GetString()!,
                StringComparer.Ordinal);
        var releasedIds = catalog.RootElement.GetProperty("releases")
            .EnumerateArray()
            .SelectMany(release => release.GetProperty("sourceDocumentIds").EnumerateArray())
            .Select(item => item.GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        var documents = manifest.RootElement.GetProperty("documents")
            .EnumerateArray()
            .ToArray();
        var documentIds = documents
            .Select(document => document.GetProperty("documentId").GetString()!)
            .ToArray();
        var expectedAuthoritativeIds = ExpectedAuthoritativeDocumentIds
            .ToHashSet(StringComparer.Ordinal);

        documentIds.Distinct(StringComparer.Ordinal).Should().HaveCount(documentIds.Length);
        documentIds.Should().BeEquivalentTo(ExpectedDocumentIds);

        foreach (var document in documents)
        {
            document.EnumerateObject().Select(property => property.Name).Should().Equal(
                "documentId",
                "sourceSha256",
                "role",
                "extractionRanges");

            var id = document.GetProperty("documentId").GetString()!;
            catalogSources.Should().ContainKey(id);
            releasedIds.Should().Contain(id);
            document.GetProperty("sourceSha256").GetString().Should()
                .Be(catalogSources[id].GetProperty("sha256").GetString());
            document.GetProperty("role").GetString().Should()
                .Be(expectedAuthoritativeIds.Contains(id) ? "authoritative" : "cross-check");

            var ranges = document.GetProperty("extractionRanges");
            ranges.ValueKind.Should().Be(JsonValueKind.Array);

            var rangeItems = ranges.EnumerateArray().ToArray();
            var rangeIds = rangeItems
                .Select(range => range.GetProperty("rangeId").GetString()!)
                .ToArray();
            foreach (var rangeId in rangeIds)
            {
                rangeId.Should().NotBeNullOrWhiteSpace();
            }

            rangeIds.Distinct(StringComparer.Ordinal).Should().HaveCount(rangeIds.Length);

            foreach (var range in rangeItems)
            {
                AssertRangeContract(range);
            }
        }
    }

    private static void AssertRangeContract(JsonElement range)
    {
        var kind = range.GetProperty("kind").GetString();
        kind.Should().BeOneOf("xlsx-rows", "pdf-pages", "html-page");
        range.GetProperty("expectedItemCount").GetInt32().Should().BeGreaterThan(0);

        switch (kind)
        {
            case "xlsx-rows":
            {
                range.EnumerateObject().Select(property => property.Name).Should().Equal(
                    "rangeId",
                    "kind",
                    "workbookOrder",
                    "rowFrom",
                    "rowTo",
                    "expectedItemCount");
                range.GetProperty("workbookOrder").GetInt32().Should().BeGreaterThan(0);
                var rowFrom = range.GetProperty("rowFrom").GetInt32();
                rowFrom.Should().BeGreaterThan(0);
                range.GetProperty("rowTo").GetInt32().Should().BeGreaterThanOrEqualTo(rowFrom);
                break;
            }
            case "pdf-pages":
            {
                range.EnumerateObject().Select(property => property.Name).Should().Equal(
                    "rangeId",
                    "kind",
                    "pageFrom",
                    "pageTo",
                    "expectedItemCount");
                var pageFrom = range.GetProperty("pageFrom").GetInt32();
                pageFrom.Should().BeGreaterThan(0);
                range.GetProperty("pageTo").GetInt32().Should().BeGreaterThanOrEqualTo(pageFrom);
                break;
            }
            case "html-page":
                range.EnumerateObject().Select(property => property.Name).Should().Equal(
                    "rangeId",
                    "kind",
                    "pageNo",
                    "expectedItemCount");
                range.GetProperty("pageNo").GetInt32().Should().BeGreaterThan(0);
                range.GetProperty("expectedItemCount").GetInt32().Should().Be(1);
                break;
        }
    }

    private static JsonDocument OpenRepositoryJson(string relativePath)
    {
        var fullPath = Path.Combine(
            TsumugiAssemblyLocator.FindSolutionRoot(),
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        return JsonDocument.Parse(File.ReadAllText(fullPath));
    }
}
