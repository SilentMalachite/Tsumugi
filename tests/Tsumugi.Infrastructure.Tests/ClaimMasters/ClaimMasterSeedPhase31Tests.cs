using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
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
        rows.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public void Source_manifest_ranges_are_machine_countable_and_fully_inventoried()
    {
        using var manifest = OpenRepositoryJson(ManifestPath);
        var root = manifest.RootElement;
        var rows = root.GetProperty("rows").EnumerateArray().ToArray();
        rows.Should().NotBeEmpty();

        var uniqueRowIds = rows.Select(row => (
            DocumentId: row.GetProperty("sourceDocumentId").GetString(),
            Locator: row.GetProperty("sourceLocator").GetString())).ToArray();
        uniqueRowIds.Should().OnlyHaveUniqueItems();

        foreach (var document in root.GetProperty("documents").EnumerateArray())
        {
            var documentId = document.GetProperty("documentId").GetString();
            var declaredRanges = document.GetProperty("extractionRanges").EnumerateArray().ToArray();
            declaredRanges.Should().NotBeEmpty(
                because: $"Task 3 must resolve every source range before transcription: {documentId}");
            foreach (var range in declaredRanges)
            {
                var rangeId = range.GetProperty("rangeId").GetString();
                var expected = range.GetProperty("expectedItemCount").GetInt32();
                rows.Count(row =>
                    row.GetProperty("sourceDocumentId").GetString() == documentId
                    && row.GetProperty("rangeId").GetString() == rangeId)
                    .Should().Be(expected);
            }
        }
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

    [Fact]
    public void Source_manifest_rows_have_closed_and_consistent_dispositions()
    {
        using var manifest = OpenRepositoryJson(ManifestPath);
        var rows = manifest.RootElement.GetProperty("rows").EnumerateArray().ToArray();

        foreach (var row in rows)
        {
            row.EnumerateObject().Select(property => property.Name).Should().Equal(
                "sourceDocumentId",
                "rangeId",
                "sourceLocator",
                "sourceLabel",
                "effectiveFrom",
                "effectiveTo",
                "disposition",
                "masterKind",
                "seedKey",
                "aggregationId",
                "aggregationKind",
                "aggregationReason",
                "exclusionReason");

            row.GetProperty("sourceLabel").GetString().Should().NotBeNullOrWhiteSpace();
            row.GetProperty("effectiveFrom").GetString().Should().NotBeNullOrWhiteSpace();
            row.GetProperty("effectiveTo").ValueKind.Should()
                .BeOneOf(JsonValueKind.String, JsonValueKind.Null);

            var disposition = row.GetProperty("disposition").GetString();
            disposition.Should().BeOneOf("seed", "excluded", "schema-gap");

            var masterKind = row.GetProperty("masterKind");
            var seedKey = row.GetProperty("seedKey");
            var aggregationId = row.GetProperty("aggregationId");
            var aggregationKind = row.GetProperty("aggregationKind");
            var aggregationReason = row.GetProperty("aggregationReason");
            var reason = row.GetProperty("exclusionReason");
            if (disposition == "seed")
            {
                masterKind.GetString().Should().BeOneOf(
                    "basic-rewards",
                    "additions",
                    "region-unit-prices",
                    "burden-caps",
                    "transition-rules",
                    "service-codes");
                seedKey.GetString().Should().NotBeNullOrWhiteSpace();
                reason.ValueKind.Should().Be(JsonValueKind.Null);
                if (aggregationId.ValueKind == JsonValueKind.Null)
                {
                    aggregationKind.ValueKind.Should().Be(JsonValueKind.Null);
                    aggregationReason.ValueKind.Should().Be(JsonValueKind.Null);
                }
                else
                {
                    aggregationId.GetString().Should().NotBeNullOrWhiteSpace();
                    aggregationKind.GetString().Should().Be("multi-source-one-seed");
                    aggregationReason.GetString().Should().NotBeNullOrWhiteSpace();
                }
            }
            else
            {
                masterKind.ValueKind.Should().Be(JsonValueKind.Null);
                seedKey.ValueKind.Should().Be(JsonValueKind.Null);
                aggregationId.ValueKind.Should().Be(JsonValueKind.Null);
                aggregationKind.ValueKind.Should().Be(JsonValueKind.Null);
                aggregationReason.ValueKind.Should().Be(JsonValueKind.Null);
                reason.GetString().Should().NotBeNullOrWhiteSpace();
            }
        }

        var seedGroups = rows
            .Where(row => row.GetProperty("disposition").GetString() == "seed")
            .GroupBy(row => (
                MasterKind: row.GetProperty("masterKind").GetString(),
                SeedKey: row.GetProperty("seedKey").GetString()))
            .ToArray();
        foreach (var group in seedGroups)
        {
            var items = group.ToArray();
            if (items.Length == 1)
            {
                items[0].GetProperty("aggregationId").ValueKind
                    .Should().Be(JsonValueKind.Null);
                continue;
            }

            var aggregationIds = items
                .Select(item => item.GetProperty("aggregationId").GetString())
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            aggregationIds.Should().ContainSingle();
            aggregationIds[0].Should().NotBeNullOrWhiteSpace();
            items.Should().OnlyContain(item =>
                item.GetProperty("aggregationKind").GetString() == "multi-source-one-seed");
            items.Select(item => item.GetProperty("aggregationReason").GetString())
                .Should().OnlyContain(item => !string.IsNullOrWhiteSpace(item));
        }
    }

    [Fact]
    public void Source_manifest_row_locators_belong_to_the_declared_document_ranges()
    {
        using var manifest = OpenRepositoryJson(ManifestPath);
        var documents = manifest.RootElement.GetProperty("documents").EnumerateArray()
            .ToDictionary(
                document => document.GetProperty("documentId").GetString()!,
                StringComparer.Ordinal);

        foreach (var row in manifest.RootElement.GetProperty("rows").EnumerateArray())
        {
            var documentId = row.GetProperty("sourceDocumentId").GetString()!;
            documents.Should().ContainKey(documentId);
            var rangeId = row.GetProperty("rangeId").GetString();
            var matchingRanges = documents[documentId].GetProperty("extractionRanges")
                .EnumerateArray()
                .Where(range => range.GetProperty("rangeId").GetString() == rangeId)
                .ToArray();
            matchingRanges.Should().ContainSingle();

            var range = matchingRanges[0];
            var locator = row.GetProperty("sourceLocator").GetString()!;
            var kind = range.GetProperty("kind").GetString();
            switch (kind)
            {
                case "xlsx-rows":
                {
                    var match = Regex.Match(
                        locator,
                        @"^workbook-order=([1-9]\d*);row=([1-9]\d*)$");
                    match.Success.Should().BeTrue();
                    var workbookOrder = int.Parse(
                        match.Groups[1].Value,
                        CultureInfo.InvariantCulture);
                    var rowNumber = int.Parse(
                        match.Groups[2].Value,
                        CultureInfo.InvariantCulture);
                    workbookOrder.Should().Be(range.GetProperty("workbookOrder").GetInt32());
                    rowNumber.Should().BeInRange(
                        range.GetProperty("rowFrom").GetInt32(),
                        range.GetProperty("rowTo").GetInt32());
                    break;
                }
                case "pdf-pages":
                {
                    var match = Regex.Match(locator, @"^pdf:physical-page=([1-9]\d*)(?:;.+)?$");
                    match.Success.Should().BeTrue();
                    var pageNumber = int.Parse(
                        match.Groups[1].Value,
                        CultureInfo.InvariantCulture);
                    pageNumber.Should().BeInRange(
                        range.GetProperty("pageFrom").GetInt32(),
                        range.GetProperty("pageTo").GetInt32());
                    break;
                }
                case "html-page":
                {
                    var match = Regex.Match(locator, @"^html:pageNo=([1-9]\d*)(?:;.+)?$");
                    match.Success.Should().BeTrue();
                    int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)
                        .Should().Be(range.GetProperty("pageNo").GetInt32());
                    if (documentId == "mhlw-unit-price-notice-observed-946c3d96")
                    {
                        locator.Should().MatchRegex(
                            @"^html:pageNo=1;table=e\d+;row=e\d+;service=e\d+$");
                    }

                    break;
                }
                default:
                    false.Should().BeTrue(because: $"unknown range kind must fail: {kind}");
                    break;
            }
        }
    }

    [Fact]
    public void Source_manifest_schema_audit_snapshot_stays_stopped()
    {
        using var manifest = OpenRepositoryJson(ManifestPath);
        var rows = manifest.RootElement.GetProperty("rows").EnumerateArray().ToArray();

        rows.Should().HaveCount(14_709);
        rows.Count(row => row.GetProperty("disposition").GetString() == "seed")
            .Should().Be(15);
        rows.Count(row => row.GetProperty("disposition").GetString() == "excluded")
            .Should().Be(744);
        var schemaGaps = rows.Where(row =>
                row.GetProperty("disposition").GetString() == "schema-gap")
            .ToArray();
        schemaGaps.Should().HaveCount(13_950).And.NotBeEmpty();

        var countsByReasonPrefix = schemaGaps
            .GroupBy(row => row.GetProperty("exclusionReason").GetString()!.Split(':')[0])
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        countsByReasonPrefix.Should().BeEquivalentTo(new Dictionary<string, int>
        {
            ["numeric-composite-unit"] = 13_539,
            ["unit-addition-or-other-operation"] = 352,
            ["condition-rate-calculation-structure"] = 59,
        });
    }

    [Fact]
    public void Source_manifest_uses_inclusive_period_boundaries_for_critical_rows()
    {
        using var manifest = OpenRepositoryJson(ManifestPath);
        var rows = manifest.RootElement.GetProperty("rows").EnumerateArray().ToArray();

        rows.Where(row =>
                row.GetProperty("sourceDocumentId").GetString()
                    == "r6-disability-support-guide-202404")
            .Should().HaveCount(4)
            .And.OnlyContain(row =>
                row.GetProperty("effectiveFrom").GetString() == "2024-04"
                && row.GetProperty("effectiveTo").ValueKind == JsonValueKind.Null);

        AssertPeriod(rows, "r6-capability-202404", "workbook-order=1;row=273", "2024-04", "2024-05");
        AssertPeriod(rows, "r6-capability-202406", "workbook-order=1;row=240", "2024-06", "2026-05");
        AssertPeriod(rows, "r8-capability-202606", "workbook-order=1;row=242", "2026-06", null);
        AssertPeriod(rows, "r6-service-codes-2-xlsx", "workbook-order=38;row=7", "2024-04", "2026-05");
        AssertPeriod(rows, "r6-service-codes-2-xlsx", "workbook-order=38;row=1061", "2024-06", "2026-05");
        AssertPeriod(rows, "r6-service-codes-2-xlsx", "workbook-order=38;row=1069", "2024-06", "2025-03");
        AssertPeriod(rows, "r6-service-codes-2-xlsx", "workbook-order=38;row=1096", "2024-06", "2025-03");
        AssertPeriod(rows, "r6-service-codes-2-xlsx", "workbook-order=38;row=1097", "2024-04", "2024-05");
        AssertPeriod(rows, "r8-service-codes-2-xlsx", "workbook-order=38;row=7", "2026-06", null);

        var r6ServiceRows = rows.Where(row =>
            row.GetProperty("sourceDocumentId").GetString() == "r6-service-codes-2-xlsx");
        r6ServiceRows.Should().OnlyContain(row =>
            (row.GetProperty("effectiveFrom").GetString() == "2024-04"
             || row.GetProperty("effectiveFrom").GetString() == "2024-06")
            && (row.GetProperty("effectiveTo").GetString() == "2024-05"
                || row.GetProperty("effectiveTo").GetString() == "2025-03"
                || row.GetProperty("effectiveTo").GetString() == "2026-05"));

        foreach (var rowNumber in Enumerable.Range(1069, 28))
        {
            AssertPeriod(
                rows,
                "r6-service-codes-2-xlsx",
                $"workbook-order=38;row={rowNumber}",
                "2024-06",
                "2025-03");
        }

        var r8ServiceRows = rows.Where(row =>
            row.GetProperty("sourceDocumentId").GetString() == "r8-service-codes-2-xlsx");
        r8ServiceRows.Should().OnlyContain(row =>
            row.GetProperty("effectiveFrom").GetString() == "2026-06"
            && row.GetProperty("effectiveTo").ValueKind == JsonValueKind.Null);
    }

    [Fact]
    public void Source_manifest_html_region_rows_and_service_gap_categories_are_precise()
    {
        using var manifest = OpenRepositoryJson(ManifestPath);
        var rows = manifest.RootElement.GetProperty("rows").EnumerateArray().ToArray();
        var regionRows = rows.Where(row =>
                row.GetProperty("sourceDocumentId").GetString()
                    == "mhlw-unit-price-notice-observed-946c3d96")
            .ToArray();

        regionRows.Should().HaveCount(8);
        regionRows.Should().OnlyContain(row =>
            row.GetProperty("disposition").GetString() == "seed"
            && row.GetProperty("masterKind").GetString() == "region-unit-prices"
            && row.GetProperty("effectiveFrom").GetString() == "2024-04"
            && row.GetProperty("effectiveTo").ValueKind == JsonValueKind.Null);
        regionRows.Select(row => row.GetProperty("seedKey").GetString())
            .Should().OnlyHaveUniqueItems();

        string[] continuationLocators =
        [
            "workbook-order=38;row=1064",
            "workbook-order=38;row=1074",
            "workbook-order=38;row=1076",
            "workbook-order=38;row=1080",
            "workbook-order=38;row=1086",
            "workbook-order=38;row=1092",
        ];
        foreach (var locator in continuationLocators)
        {
            var row = FindRow(rows, "r6-service-codes-2-xlsx", locator);
            row.GetProperty("disposition").GetString().Should().Be("excluded");
            row.GetProperty("exclusionReason").GetString().Should().Contain("継続行");
        }

        foreach (var locator in new[]
                 {
                     "workbook-order=38;row=2266",
                     "workbook-order=38;row=2268",
                 })
        {
            var row = FindRow(rows, "r8-service-codes-2-xlsx", locator);
            row.GetProperty("disposition").GetString().Should().Be("excluded");
            row.GetProperty("exclusionReason").GetString().Should().Contain("継続行");
        }

        var allowedGapReasons = new[]
        {
            "numeric-composite-unit:",
            "unit-addition-or-other-operation:",
            "condition-rate-calculation-structure:",
        };
        rows.Where(row =>
                row.GetProperty("disposition").GetString() == "schema-gap"
                && row.GetProperty("sourceDocumentId").GetString()!.EndsWith(
                    "service-codes-2-xlsx",
                    StringComparison.Ordinal))
            .Should().OnlyContain(row => allowedGapReasons.Any(prefix =>
                row.GetProperty("exclusionReason").GetString()!.StartsWith(
                    prefix,
                    StringComparison.Ordinal)));
    }

    private static void AssertPeriod(
        IEnumerable<JsonElement> rows,
        string documentId,
        string locator,
        string effectiveFrom,
        string? effectiveTo)
    {
        var row = FindRow(rows, documentId, locator);
        row.GetProperty("effectiveFrom").GetString().Should().Be(effectiveFrom);
        if (effectiveTo is null)
        {
            row.GetProperty("effectiveTo").ValueKind.Should().Be(JsonValueKind.Null);
        }
        else
        {
            row.GetProperty("effectiveTo").GetString().Should().Be(effectiveTo);
        }
    }

    private static JsonElement FindRow(
        IEnumerable<JsonElement> rows,
        string documentId,
        string locator) => rows.Single(row =>
            row.GetProperty("sourceDocumentId").GetString() == documentId
            && row.GetProperty("sourceLocator").GetString() == locator);

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
