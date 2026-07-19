using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Tsumugi.Infrastructure.ClaimMasters;
using Tsumugi.Infrastructure.Tests;

namespace Tsumugi.Infrastructure.Tests.ClaimMasters;

public sealed class ClaimMasterSeedPhase31Tests
{
    // Task 3 (seed実値投入): verify JsonClaimMasterProvider.LoadEmbedded() validates
    // successfully once basic-rewards.json / service-codes.json / region-unit-prices.json
    // carry ADR 0027's R6 values. ResolveCalculationMasters-based row-count assertions are
    // deferred to Task 4 per the phase3-1 task-3 brief.
    [Fact]
    public void LoadEmbedded_succeeds_with_populated_seeds()
    {
        var action = () => JsonClaimMasterProvider.LoadEmbedded();

        action.Should().NotThrow();
    }

    // ADR 0027 fixes 135 basic-reward rows (service fee I-III: 3 staffing x 5 capacity x
    // 8 wage bands = 120, plus participation-evaluation IV-VI: 15), a matching 135
    // service-code rows (one per basic-reward row), and 8 region unit prices. Task 11
    // (ADR 0028) adds one service-code row per seeded addition row on top of the 135
    // (see ClaimAdditionSeedScopeTests for the per-code scope). This asserts the seed JSON
    // itself (not the not-yet-wired ResolveCalculationMasters) carries those counts.
    [Fact]
    public void LoadEmbedded_embeds_the_adr0027_r6_seed_row_counts()
    {
        CountEmbeddedEntries(".ClaimMasters.Seed.basic-rewards.json").Should().Be(135);
        var additionEntries = CountEmbeddedEntries(".ClaimMasters.Seed.additions.json");
        CountEmbeddedEntries(".ClaimMasters.Seed.service-codes.json")
            .Should().Be(135 + additionEntries);
        CountEmbeddedEntries(".ClaimMasters.Seed.region-unit-prices.json").Should().Be(8);
    }

    private static int CountEmbeddedEntries(string suffix)
    {
        var assembly = typeof(JsonClaimMasterProvider).Assembly;
        var names = assembly.GetManifestResourceNames()
            .Where(name => name.EndsWith(suffix, StringComparison.Ordinal))
            .ToArray();
        names.Should().ContainSingle();
        using var stream = assembly.GetManifestResourceStream(names[0])!;
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.GetProperty("entries").GetArrayLength();
    }

    private const string ExpectedFinalOrderedIdentityDigest =
        "c80f4e8da0aefc9d91bd978777bdb8e59261f4982826555f8a324e2023b9bcd7";

    private const string ExpectedBaselineOrderedIdentityDigest =
        "90fb9d309e878d22f0d4bb867c4fe36c3fab83ad45938b64da2d5b3bfd34dee7";

    private const string PreviousOrderedIdentityDigest =
        "e350d208f9862fa2a4a0328df7e3ad6bca145b5e8dfde38a63a0c4d79126d225";

    private static readonly string[] TreatmentImprovementEvidenceLocators =
    [
        "pdf:physical-page=36;section=legacy-treatment-improvement",
        "pdf:physical-page=36;section=integrated-treatment-improvement-v",
        "pdf:physical-page=36;section=integrated-treatment-improvement-i-iv",
    ];

    private static readonly string[] TreatmentImprovementCalculationLocators =
    [
        "pdf:physical-page=8;section=legacy-treatment-improvement",
        "pdf:physical-page=8;section=integrated-treatment-improvement-v",
        "pdf:physical-page=8;section=integrated-treatment-improvement-i-iv",
    ];

    private const string ManifestPath =
        "docs/spec-data/phase3/claim-master-source-row-manifest.json";

    private static readonly JsonSerializerOptions IdentityJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly string[] AllowedTargetKinds =
    [
        "basic-rewards",
        "additions",
        "region-unit-prices",
        "burden-caps",
        "transition-rules",
        "service-codes",
        "service-code-conditions",
    ];

    private static readonly string[] AllowedMappingRoles =
    [
        "primary", "component", "supporting-evidence",
    ];

    private static readonly string[] AllowedSupports =
    [
        "service-identity", "selectors", "unit-rule-kind", "unit-rule-value",
        "unit-rule-target", "unit-rule-step", "unit-rule-rounding", "conditions",
        "effective-period", "master-values",
        "unit-rule-formula", "unit-rule-comparison", "unit-rule-local-government-adjustment",
        "unit-rule-runtime-input", "unit-rule-runtime-input-provenance",
    ];

    private static readonly string[] ProtectedFacilityServiceCodes =
    [
        "462841", "462842", "462843", "462844", "462845", "462846",
        "46C841", "46C842", "46C843", "46C844", "46C845", "46C846",
        "46D841", "46D842", "46D843", "46D844", "46D845", "46D846",
        "46E841", "46E844", "46F841", "46F844",
    ];

    private static readonly string[] ProtectedFacilityEvidenceRangeIds =
    [
        "r6-protected-facility-b-comparison",
        "r6-protected-facility-b-local-government",
        "r6-protected-facility-b-current-consolidated",
        "r8-protected-facility-b-current-consolidated",
        "r6-protected-facility-administrative-expense-provenance",
        "r8-protected-facility-administrative-expense-provenance",
        "r6-protected-facility-b-formula-continuity",
        "r6-protected-facility-b-local-government-continuity",
    ];

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
        "current-fee-notice-html",
        "protected-facility-administrative-expense-standard-html",
        "h31-fee-notice-consolidated",
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
        "current-fee-notice-html",
        "protected-facility-administrative-expense-standard-html",
    ];

    [Fact]
    public void Source_manifest_exists_and_has_a_closed_v2_contract()
    {
        using var manifest = OpenRepositoryJson(ManifestPath);
        var root = manifest.RootElement;

        root.EnumerateObject().Select(property => property.Name)
            .Should().Equal("schemaVersion", "documents", "rows");
        root.GetProperty("schemaVersion").GetString().Should().Be("2");
        root.GetProperty("documents").GetArrayLength().Should().Be(44);
        root.GetProperty("rows").GetArrayLength().Should().Be(14_726);

        foreach (var row in root.GetProperty("rows").EnumerateArray())
        {
            row.EnumerateObject().Select(property => property.Name).Should().Equal(
                "sourceDocumentId",
                "rangeId",
                "sourceLocator",
                "sourceLabel",
                "effectiveFrom",
                "effectiveTo",
                "disposition",
                "productionTargets",
                "exclusionReason");

            row.GetProperty("sourceLabel").GetString().Should().NotBeNullOrWhiteSpace();
            row.GetProperty("effectiveFrom").GetString().Should().NotBeNullOrWhiteSpace();
            row.GetProperty("effectiveTo").ValueKind.Should()
                .BeOneOf(JsonValueKind.String, JsonValueKind.Null);
        }
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
            RangeId: row.GetProperty("rangeId").GetString(),
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
    public void Source_manifest_v2_targets_have_closed_roles_and_supports()
    {
        using var manifest = OpenRepositoryJson(ManifestPath);
        foreach (var row in manifest.RootElement.GetProperty("rows").EnumerateArray())
        {
            var rowContext = $"manifest row "
                             + $"{row.GetProperty("sourceDocumentId").GetString()} / "
                             + $"{row.GetProperty("rangeId").GetString()} / "
                             + row.GetProperty("sourceLocator").GetString();
            var disposition = row.GetProperty("disposition").GetString();
            var targets = GetProductionTargets(row);
            var reason = row.GetProperty("exclusionReason");

            if (disposition == "seed")
            {
                targets.Should().NotBeEmpty(because: rowContext);
                reason.ValueKind.Should().Be(JsonValueKind.Null);
            }
            else
            {
                disposition.Should().BeOneOf("excluded", "schema-gap");
                targets.Should().BeEmpty(because: rowContext);
                reason.GetString().Should().NotBeNullOrWhiteSpace();
            }

            foreach (var target in targets)
            {
                var targetContext = $"target contract for {rowContext}";
                target.EnumerateObject().Select(property => property.Name).Should().Equal(
                    ["masterKind", "seedKey", "mappingRole", "supports", "mappingReason"],
                    because: targetContext);
                target.GetProperty("masterKind").ValueKind.Should().Be(
                    JsonValueKind.String,
                    because: targetContext);
                target.GetProperty("masterKind").GetString().Should().BeOneOf(
                    AllowedTargetKinds,
                    because: targetContext);
                target.GetProperty("seedKey").ValueKind.Should().Be(
                    JsonValueKind.String,
                    because: targetContext);
                target.GetProperty("seedKey").GetString().Should().NotBeNullOrWhiteSpace(
                    because: targetContext);
                target.GetProperty("mappingRole").ValueKind.Should().Be(
                    JsonValueKind.String,
                    because: targetContext);
                var role = target.GetProperty("mappingRole").GetString();
                role.Should().BeOneOf(AllowedMappingRoles, because: targetContext);
                var supportsElement = target.GetProperty("supports");
                supportsElement.ValueKind.Should().Be(
                    JsonValueKind.Array,
                    because: targetContext);
                var supportElements = supportsElement.EnumerateArray().ToArray();
                supportElements.Should().OnlyContain(
                    item => item.ValueKind == JsonValueKind.String,
                    because: targetContext);
                var supports = supportElements
                    .Select(item => item.GetString()!).ToArray();
                supports.Should().NotBeEmpty(because: targetContext)
                    .And.OnlyHaveUniqueItems(because: targetContext);
                supports.Should().OnlyContain(
                    support => AllowedSupports.Contains(support),
                    because: targetContext);
                var mappingReason = target.GetProperty("mappingReason");
                mappingReason.ValueKind.Should().BeOneOf(
                    [JsonValueKind.String, JsonValueKind.Null],
                    because: targetContext);
                if (mappingReason.ValueKind == JsonValueKind.String)
                {
                    mappingReason.GetString().Should().NotBeNullOrWhiteSpace(
                        because: targetContext);
                }

                if (role is "component" or "supporting-evidence")
                {
                    mappingReason.ValueKind.Should().Be(
                        JsonValueKind.String,
                        because: targetContext);
                }
            }
        }
    }

    [Fact]
    public void Source_manifest_v2_includes_the_approved_source_inventory_correction()
    {
        using var manifest = OpenRepositoryJson(ManifestPath);
        var root = manifest.RootElement;
        root.GetProperty("documents").GetArrayLength().Should().Be(44);
        root.GetProperty("rows").GetArrayLength().Should().Be(14_726);
        var ranges = root.GetProperty("documents").EnumerateArray()
            .SelectMany(document => document.GetProperty("extractionRanges").EnumerateArray())
            .ToArray();
        ranges.Should().HaveCount(61);
        ranges.Sum(range => range.GetProperty("expectedItemCount").GetInt32())
            .Should().Be(14_726);
    }

    [Fact]
    public void Source_manifest_inventories_the_percentage_evidence_pages_before_reaudit()
    {
        using var manifest = OpenRepositoryJson(ManifestPath);
        var root = manifest.RootElement;
        var documents = root.GetProperty("documents").EnumerateArray().ToArray();
        var rows = root.GetProperty("rows").EnumerateArray().ToArray();

        AssertPdfRange(
            documents,
            "r6-fee-notice",
            "r6-b-percentage-reductions",
            141,
            142,
            2);
        AssertPdfRange(
            documents,
            "r8-fee-notice",
            "r8-b-emergency-rate-and-reduction-continuity",
            56,
            56,
            1);

        foreach (var (documentId, locator) in new[]
                 {
                     ("r6-fee-notice", "pdf:physical-page=141"),
                     ("r6-fee-notice", "pdf:physical-page=142"),
                     ("r8-fee-notice", "pdf:physical-page=56"),
                 })
        {
            var row = FindRow(rows, documentId, locator);
            row.GetProperty("disposition").GetString().Should().Be("seed");
            GetProductionTargets(row).Should().NotBeEmpty();
            row.GetProperty("exclusionReason").ValueKind.Should().Be(JsonValueKind.Null);
        }
    }

    [Fact]
    public void Source_manifest_v2_preserves_the_ordered_row_identity_digest()
    {
        using var manifest = OpenRepositoryJson(ManifestPath);
        var rows = manifest.RootElement.GetProperty("rows").EnumerateArray().ToArray();

        CalculateOrderedIdentityDigest(rows).Should().Be(ExpectedFinalOrderedIdentityDigest);
        CalculateOrderedIdentityDigest(rows.Take(14_718)).Should()
            .Be(ExpectedBaselineOrderedIdentityDigest,
                because: "the protected-facility evidence rows must append to the baseline");
        CalculateOrderedIdentityDigest(rows.Take(14_718).Where(row =>
                !TreatmentImprovementCalculationLocators.Contains(
                    row.GetProperty("sourceLocator").GetString(),
                    StringComparer.Ordinal)))
            .Should().Be(PreviousOrderedIdentityDigest,
                because: "the three approved calculation rows must not reorder the previous inventory");
    }

    [Fact]
    public void Source_manifest_inventories_period_specific_treatment_improvement_evidence()
    {
        using var manifest = OpenRepositoryJson(ManifestPath);
        var root = manifest.RootElement;
        var documents = root.GetProperty("documents").EnumerateArray().ToArray();
        var rows = root.GetProperty("rows").EnumerateArray().ToArray();

        AssertPdfRange(
            documents,
            "r6-reward-structure",
            "r6-b-reward-structure",
            34,
            36,
            6);

        var broadPageIndex = Array.FindIndex(rows, row =>
            row.GetProperty("sourceDocumentId").GetString() == "r6-reward-structure"
            && row.GetProperty("sourceLocator").GetString() == "pdf:physical-page=36");
        broadPageIndex.Should().BeGreaterThanOrEqualTo(0);
        rows.Skip(broadPageIndex + 1)
            .Take(TreatmentImprovementEvidenceLocators.Length)
            .Select(row => row.GetProperty("sourceLocator").GetString())
            .Should().Equal(TreatmentImprovementEvidenceLocators);

        foreach (var (locator, effectiveFrom, effectiveTo) in new[]
                 {
                     (TreatmentImprovementEvidenceLocators[0], "2024-04", "2024-05"),
                     (TreatmentImprovementEvidenceLocators[1], "2024-06", "2025-03"),
                     (TreatmentImprovementEvidenceLocators[2], "2024-06", "2026-05"),
                 })
        {
            var row = FindRow(rows, "r6-reward-structure", locator);
            row.GetProperty("rangeId").GetString().Should().Be("r6-b-reward-structure");
            row.GetProperty("effectiveFrom").GetString().Should().Be(effectiveFrom);
            row.GetProperty("effectiveTo").GetString().Should().Be(effectiveTo);
            row.GetProperty("disposition").GetString().Should().Be("seed");
            GetProductionTargets(row).Should().NotBeEmpty();
            row.GetProperty("exclusionReason").ValueKind.Should().Be(JsonValueKind.Null);
        }
    }

    [Fact]
    public void Source_manifest_inventories_period_specific_treatment_improvement_calculation_evidence()
    {
        using var manifest = OpenRepositoryJson(ManifestPath);
        var root = manifest.RootElement;
        var documents = root.GetProperty("documents").EnumerateArray().ToArray();
        var rows = root.GetProperty("rows").EnumerateArray().ToArray();

        AssertPdfRange(
            documents,
            "r6-calculation-note",
            "r6-unit-and-cost-rounding",
            8,
            9,
            5);

        var broadPageIndex = Array.FindIndex(rows, row =>
            row.GetProperty("sourceDocumentId").GetString() == "r6-calculation-note"
            && row.GetProperty("sourceLocator").GetString() == "pdf:physical-page=8");
        broadPageIndex.Should().BeGreaterThanOrEqualTo(0);
        rows.Skip(broadPageIndex + 1)
            .Take(TreatmentImprovementCalculationLocators.Length)
            .Select(row => row.GetProperty("sourceLocator").GetString())
            .Should().Equal(TreatmentImprovementCalculationLocators);

        foreach (var (locator, effectiveFrom, effectiveTo) in new[]
                 {
                     (TreatmentImprovementCalculationLocators[0], "2024-04", "2024-05"),
                     (TreatmentImprovementCalculationLocators[1], "2024-06", "2025-03"),
                     (TreatmentImprovementCalculationLocators[2], "2024-06", "2026-05"),
                 })
        {
            var row = FindRow(rows, "r6-calculation-note", locator);
            row.GetProperty("rangeId").GetString().Should().Be("r6-unit-and-cost-rounding");
            row.GetProperty("effectiveFrom").GetString().Should().Be(effectiveFrom);
            row.GetProperty("effectiveTo").GetString().Should().Be(effectiveTo);
            row.GetProperty("disposition").GetString().Should().Be("seed");
            GetProductionTargets(row).Should().NotBeEmpty();
            row.GetProperty("exclusionReason").ValueKind.Should().Be(JsonValueKind.Null);
        }
    }

    [Fact]
    public void Ordered_row_identity_digest_detects_reordering()
    {
        using var manifest = OpenRepositoryJson(ManifestPath);
        var rows = manifest.RootElement.GetProperty("rows").EnumerateArray().Take(2).ToArray();

        CalculateOrderedIdentityDigest(rows).Should().NotBe(
            CalculateOrderedIdentityDigest(rows.Reverse()),
            because: "the manifest digest must detect row reordering");
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
                case "html-lines":
                    {
                        locator.Should().MatchRegex(
                            @"^html:lines=l\d+(?:-l\d+)?(?:,l\d+(?:-l\d+)?)*$");
                        var lineFrom = range.GetProperty("lineFrom").GetInt32();
                        var lineTo = range.GetProperty("lineTo").GetInt32();
                        foreach (Match lineMatch in Regex.Matches(
                                     locator,
                                     @"l(\d+)(?:-l(\d+))?"))
                        {
                            var locatorFrom = int.Parse(
                                lineMatch.Groups[1].Value,
                                CultureInfo.InvariantCulture);
                            var locatorTo = lineMatch.Groups[2].Success
                                ? int.Parse(
                                    lineMatch.Groups[2].Value,
                                    CultureInfo.InvariantCulture)
                                : locatorFrom;
                            locatorFrom.Should().BeInRange(lineFrom, lineTo);
                            locatorTo.Should().BeInRange(locatorFrom, lineTo);
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
    public void Source_manifest_maps_the_protected_facility_rows_and_evidence_by_period()
    {
        using var manifest = OpenRepositoryJson(ManifestPath);
        var rows = manifest.RootElement.GetProperty("rows").EnumerateArray().ToArray();
        var expectedSeedKeys = ProtectedFacilityServiceCodes
            .Select(code => $"service-code-{code}")
            .ToHashSet(StringComparer.Ordinal);

        var primaryRows = rows.Where(IsProtectedFacilityPrimaryRow).ToArray();
        primaryRows.Should().HaveCount(44);
        primaryRows.Count(row => row.GetProperty("sourceDocumentId").GetString()
                == "r6-service-codes-2-xlsx")
            .Should().Be(22);
        primaryRows.Count(row => row.GetProperty("sourceDocumentId").GetString()
                == "r8-service-codes-2-xlsx")
            .Should().Be(22);

        foreach (var documentId in new[]
                 {
                     "r6-service-codes-2-xlsx",
                     "r8-service-codes-2-xlsx",
                 })
        {
            var documentRows = primaryRows.Where(row =>
                    row.GetProperty("sourceDocumentId").GetString() == documentId)
                .ToArray();
            documentRows.SelectMany(GetProductionTargets)
                .Where(target => target.GetProperty("mappingRole").GetString() == "primary")
                .Select(target => target.GetProperty("seedKey").GetString()!)
                .Should().BeEquivalentTo(expectedSeedKeys);

            foreach (var row in documentRows)
            {
                row.GetProperty("disposition").GetString().Should().Be("seed");
                var primaryTargets = GetProductionTargets(row).Where(target =>
                        target.GetProperty("mappingRole").GetString() == "primary")
                    .ToArray();
                primaryTargets.Should().ContainSingle();
                expectedSeedKeys.Should().Contain(
                    primaryTargets[0].GetProperty("seedKey").GetString()!);
                if (documentId.StartsWith("r6-", StringComparison.Ordinal))
                {
                    row.GetProperty("effectiveFrom").GetString().Should().Be("2024-04");
                    row.GetProperty("effectiveTo").GetString().Should().Be("2026-05");
                    primaryTargets[0].GetProperty("mappingReason").GetString()
                        .Should().NotBeNullOrWhiteSpace();
                }
                else
                {
                    row.GetProperty("effectiveFrom").GetString().Should().Be("2026-06");
                    row.GetProperty("effectiveTo").ValueKind.Should().Be(JsonValueKind.Null);
                }
            }
        }

        var evidenceRows = rows.Where(row =>
                ProtectedFacilityEvidenceRangeIds.Contains(
                    row.GetProperty("rangeId").GetString(),
                    StringComparer.Ordinal))
            .ToArray();
        evidenceRows.Should().HaveCount(8);
        evidenceRows.Select(row => row.GetProperty("rangeId").GetString())
            .Should().Equal(ProtectedFacilityEvidenceRangeIds);
        foreach (var row in evidenceRows)
        {
            row.GetProperty("disposition").GetString().Should().Be("seed");
            var targets = GetProductionTargets(row);
            targets.Should().HaveCount(22);
            targets.Should().OnlyContain(target =>
                target.GetProperty("masterKind").GetString() == "service-codes"
                && target.GetProperty("mappingRole").GetString() == "supporting-evidence"
                && !string.IsNullOrWhiteSpace(
                    target.GetProperty("mappingReason").GetString()));
            targets.Select(target => target.GetProperty("seedKey").GetString()!)
                .Should().BeEquivalentTo(expectedSeedKeys);

            if (row.GetProperty("rangeId").GetString()!.StartsWith(
                    "r8-",
                    StringComparison.Ordinal))
            {
                row.GetProperty("effectiveFrom").GetString().Should().Be("2026-06");
                row.GetProperty("effectiveTo").ValueKind.Should().Be(JsonValueKind.Null);
            }
            else
            {
                row.GetProperty("effectiveFrom").GetString().Should().Be("2024-04");
                row.GetProperty("effectiveTo").GetString().Should().Be("2026-05");
            }
        }

        evidenceRows.Where(row => row.GetProperty("sourceDocumentId").GetString()
                == "h31-fee-notice-consolidated")
            .Should().HaveCount(2)
            .And.OnlyContain(row =>
                row.GetProperty("effectiveTo").GetString() == "2026-05");

        foreach (var (documentId, locator, effectiveFrom) in new[]
                 {
                     ("r6-calculation-note", "pdf:physical-page=8", "2024-04"),
                     ("r6-calculation-note", "pdf:physical-page=9", "2024-04"),
                     ("r8-calculation-note", "pdf:physical-page=8", "2026-06"),
                     ("r8-calculation-note", "pdf:physical-page=9", "2026-06"),
                     ("r8-fee-notice", "pdf:physical-page=56", "2026-06"),
                 })
        {
            var row = FindRow(rows, documentId, locator);
            row.GetProperty("effectiveFrom").GetString().Should().Be(effectiveFrom);
            GetProductionTargets(row).Where(target =>
                    target.GetProperty("masterKind").GetString() == "service-codes"
                    && expectedSeedKeys.Contains(target.GetProperty("seedKey").GetString()!))
                .Should().HaveCount(22)
                .And.OnlyContain(target =>
                    target.GetProperty("mappingRole").GetString() == "supporting-evidence");
        }
    }

    [Fact]
    public void Source_manifest_v2_has_no_schema_gaps_before_seed_transcription()
    {
        using var manifest = OpenRepositoryJson(ManifestPath);
        manifest.RootElement.GetProperty("rows").EnumerateArray()
            .Should().NotContain(row =>
                row.GetProperty("disposition").GetString() == "schema-gap");
    }

    [Fact]
    public void Source_manifest_v2_inventory_is_finalized_without_schema_gaps()
    {
        using var manifest = OpenRepositoryJson(ManifestPath);
        var rows = manifest.RootElement.GetProperty("rows").EnumerateArray().ToArray();

        rows.Should().HaveCount(14_726);
        rows.Count(row => row.GetProperty("disposition").GetString() == "seed")
            .Should().Be(14_189);
        rows.Count(row => row.GetProperty("disposition").GetString() == "excluded")
            .Should().Be(537);
        rows.Count(row =>
                row.GetProperty("disposition").GetString() == "schema-gap")
            .Should().Be(0);
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
        var regionTargets = new List<JsonElement>();
        foreach (var row in regionRows)
        {
            row.GetProperty("disposition").GetString().Should().Be("seed");
            row.GetProperty("effectiveFrom").GetString().Should().Be("2024-04");
            row.GetProperty("effectiveTo").ValueKind.Should().Be(JsonValueKind.Null);

            var primaryTargets = GetProductionTargets(row)
                .Where(target =>
                    target.GetProperty("masterKind").GetString() == "region-unit-prices"
                    && target.GetProperty("mappingRole").GetString() == "primary")
                .ToArray();
            primaryTargets.Should().ContainSingle(
                because: $"region source row {row.GetProperty("sourceLocator").GetString()} "
                         + "must map to exactly one primary region-unit-prices target");
            regionTargets.Add(primaryTargets.Single());
        }

        var regionSeedKeys = regionTargets
            .Select(target => target.GetProperty("seedKey").GetString())
            .ToArray();
        regionSeedKeys.Should().OnlyContain(seedKey => !string.IsNullOrWhiteSpace(seedKey));
        regionSeedKeys.Should().OnlyHaveUniqueItems();

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

        rows.Where(row =>
                row.GetProperty("disposition").GetString() == "schema-gap"
                && row.GetProperty("sourceDocumentId").GetString()!.EndsWith(
                    "service-codes-2-xlsx",
                    StringComparison.Ordinal))
            .Should().BeEmpty();
    }

    private static bool IsProtectedFacilityPrimaryRow(JsonElement row)
    {
        var locator = row.GetProperty("sourceLocator").GetString();
        var match = Regex.Match(locator ?? string.Empty, @"^workbook-order=(\d+);row=(\d+)$");
        if (!match.Success)
        {
            return false;
        }

        var workbookOrder = int.Parse(
            match.Groups[1].Value,
            CultureInfo.InvariantCulture);
        var sourceRow = int.Parse(
            match.Groups[2].Value,
            CultureInfo.InvariantCulture);
        var documentId = row.GetProperty("sourceDocumentId").GetString();
        var rangeId = row.GetProperty("rangeId").GetString();

        return (documentId, rangeId, workbookOrder) switch
        {
            ("r6-service-codes-2-xlsx", "r6-b-basic", 38) => sourceRow is >= 907 and <= 912,
            ("r6-service-codes-2-xlsx", "r6-b-support-staff-shortage", 40) =>
                sourceRow is >= 1807 and <= 1818,
            ("r6-service-codes-2-xlsx", "r6-b-service-manager-shortage", 41) =>
                sourceRow is >= 607 and <= 610,
            ("r8-service-codes-2-xlsx", "r8-b-basic", 38) => sourceRow is >= 1987 and <= 1992,
            ("r8-service-codes-2-xlsx", "r8-b-support-staff-shortage", 40) =>
                sourceRow is >= 3967 and <= 3978,
            ("r8-service-codes-2-xlsx", "r8-b-service-manager-shortage", 41) =>
                sourceRow is >= 1327 and <= 1330,
            _ => false,
        };
    }

    private static JsonElement[] GetProductionTargets(JsonElement row)
    {
        var documentId = row.GetProperty("sourceDocumentId").GetString();
        var sourceLocator = row.GetProperty("sourceLocator").GetString();
        var hasProductionTargets = row.TryGetProperty(
            "productionTargets",
            out var productionTargets);

        hasProductionTargets.Should().BeTrue(
            because: $"schema v2 requires productionTargets for {documentId} / {sourceLocator}");
        productionTargets.ValueKind.Should().Be(
            JsonValueKind.Array,
            because: $"schema v2 requires productionTargets to be an array for "
                     + $"{documentId} / {sourceLocator}");
        return productionTargets.EnumerateArray().ToArray();
    }

    private static string CalculateOrderedIdentityDigest(IEnumerable<JsonElement> rows)
    {
        var payload = new StringBuilder();
        foreach (var row in rows)
        {
            string[] identity =
            [
                row.GetProperty("sourceDocumentId").GetString()!,
                row.GetProperty("rangeId").GetString()!,
                row.GetProperty("sourceLocator").GetString()!,
            ];
            payload.Append(JsonSerializer.Serialize(identity, IdentityJsonOptions));
            payload.Append('\n');
        }

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(payload.ToString()));
        return Convert.ToHexStringLower(digest);
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

    private static void AssertPdfRange(
        IEnumerable<JsonElement> documents,
        string documentId,
        string rangeId,
        int pageFrom,
        int pageTo,
        int expectedItemCount)
    {
        var document = documents.Single(item =>
            item.GetProperty("documentId").GetString() == documentId);
        var range = document.GetProperty("extractionRanges").EnumerateArray().Single(item =>
            item.GetProperty("rangeId").GetString() == rangeId);

        range.GetProperty("kind").GetString().Should().Be("pdf-pages");
        range.GetProperty("pageFrom").GetInt32().Should().Be(pageFrom);
        range.GetProperty("pageTo").GetInt32().Should().Be(pageTo);
        range.GetProperty("expectedItemCount").GetInt32().Should().Be(expectedItemCount);
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
        kind.Should().BeOneOf("xlsx-rows", "pdf-pages", "html-page", "html-lines");
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
            case "html-lines":
                range.EnumerateObject().Select(property => property.Name).Should().Equal(
                    "rangeId",
                    "kind",
                    "lineFrom",
                    "lineTo",
                    "expectedItemCount");
                var lineFrom = range.GetProperty("lineFrom").GetInt32();
                lineFrom.Should().BeGreaterThan(0);
                range.GetProperty("lineTo").GetInt32().Should().BeGreaterThanOrEqualTo(lineFrom);
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
