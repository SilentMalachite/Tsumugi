using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Tsumugi.Domain.ValueObjects;
using Tsumugi.Infrastructure.ClaimMasters;

namespace Tsumugi.Infrastructure.Tests.ClaimMasters;

public sealed class JsonClaimMasterProviderTests
{
    private const string Sha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private static readonly string[] ExpectedSourceIds =
    [
        "mhlw-r6-revision-page-observed-c7b78655",
        "mhlw-r6-revision-page-observed-fefe2d88",
        "mhlw-r6-structure-page-observed-8a9858bf",
        "mhlw-r8-revision-page-observed-2e8f1425",
        "mhlw-r8-revision-page-observed-7c937a6a",
        "mhlw-r8-structure-page-observed-9bc71ce6",
        "mhlw-r8-structure-page-observed-13da3c44",
        "mhlw-unit-price-notice-observed-946c3d96",
        "r6-disability-support-guide-202404",
        "r6-claim-handbook-202405",
        "r6-grant-decision-administration-202404",
        "r7-grant-decision-administration-202501",
        "r7-grant-decision-administration-202509",
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
        "r6-capability-202404",
        "r6-capability-202406",
        "r6-reward-structure",
        "r6-service-codes-1-pdf",
        "r6-service-codes-1-xlsx",
        "r6-service-codes-2-pdf",
        "r6-service-codes-2-xlsx",
        "r6-claim-decision-202404-pdf",
        "r6-claim-decision-202404-xls",
        "r6-claim-decision-202406-pdf",
        "r6-claim-decision-202406-xls",
        "r8-grant-decision-administration-202606",
        "r8-revision-overview",
        "r8-fee-notice",
        "r8-calculation-note",
        "r8-b-reward-band-guide",
        "r8-employment-transition-guide",
        "r8-qa-v1",
        "r8-amendment-qa",
        "r8-capability-202606",
        "r8-capability-correction",
        "r8-reward-structure",
        "r8-service-codes-1-pdf",
        "r8-service-codes-1-xlsx",
        "r8-service-codes-2-pdf",
        "r8-service-codes-2-xlsx",
        "r8-claim-decision-pdf",
        "r8-claim-decision-xls",
        "current-fee-notice-html",
        "protected-facility-administrative-expense-standard-html",
        "h31-fee-notice-consolidated",
    ];

    public static TheoryData<int, int, string> EmbeddedReleaseCases => new()
    {
        { 2024, 4, "claim-master-r6-04" },
        { 2024, 5, "claim-master-r6-04" },
        { 2024, 6, "claim-master-r6-06" },
        { 2024, 12, "claim-master-r6-06" },
        { 2025, 1, "claim-master-r7-01" },
        { 2025, 8, "claim-master-r7-01" },
        { 2025, 9, "claim-master-r7-09" },
        { 2026, 5, "claim-master-r7-09" },
        { 2026, 6, "claim-master-r8-06" },
        { 2200, 12, "claim-master-r8-06" },
    };

    public static TheoryData<string, string> EmbeddedGrantSourceCases => new()
    {
        { "claim-master-r6-04", "r6-grant-decision-administration-202404" },
        { "claim-master-r6-06", "r6-grant-decision-administration-202404" },
        { "claim-master-r7-01", "r7-grant-decision-administration-202501" },
        { "claim-master-r7-09", "r7-grant-decision-administration-202509" },
        { "claim-master-r8-06", "r8-grant-decision-administration-202606" },
    };

    public static TheoryData<string, string> FileKindMappings => new()
    {
        { "basic-rewards.json", "basic-rewards" },
        { "additions.json", "additions" },
        { "region-unit-prices.json", "region-unit-prices" },
        { "burden-caps.json", "burden-caps" },
        { "transition-rules.json", "transition-rules" },
        { "service-codes.json", "service-codes" },
    };

    public static IEnumerable<object[]> InvalidCatalogCases()
    {
        yield return ["schema mismatch", MutateCatalog(root => root["schemaVersion"] = "2"), false];
        yield return ["duplicate source", MutateCatalog(root =>
        {
            var sources = root["sources"]!.AsArray();
            sources.Add(sources[0]!.DeepClone());
        }), false];
        yield return ["blank required", MutateSource(source => source["title"] = " "), false];
        yield return ["outer whitespace", MutateSource(source => source["publisher"] = " Ministry "), false];
        yield return ["invalid sha", MutateSource(source => source["sha256"] = Sha256.ToUpperInvariant()), false];
        yield return ["invalid date", MutateSource(source => source["effectiveAt"] = "2024-02-30"), false];
        yield return ["unknown supersedes", MutateSource(source => source["supersedes"] = new JsonArray("missing")), false];
        yield return ["unknown corrects", MutateSource(source => source["corrects"] = new JsonArray("missing")), false];
        yield return ["unknown supplements", MutateSource(source => source["supplements"] = new JsonArray("missing")), false];
        yield return ["unknown release source", MutateCatalog(root =>
            root["releases"]![0]!["sourceDocumentIds"] = new JsonArray("missing")), true];
        yield return ["required null", MutateSource(source => source["documentId"] = null), false];
        yield return ["case mismatch", ValidCatalogJson.Replace("\"documentId\"", "\"DocumentId\"", StringComparison.Ordinal), false];
        yield return ["unknown top-level", MutateCatalog(root => root["unknown"] = true), false];
        yield return ["unknown source", MutateSource(source => source["unknown"] = true), false];
        yield return ["unknown release", MutateCatalog(root => root["releases"]![0]!["unknown"] = true), false];
        yield return ["duplicate property", ValidCatalogJson.Replace(
            "\"schemaVersion\": \"1\"",
            "\"schemaVersion\": \"1\", \"schemaVersion\": \"1\"",
            StringComparison.Ordinal), false];
    }

    public static IEnumerable<object[]> InvalidMasterCases()
    {
        yield return ["schema mismatch", MasterJson("basic-rewards", "[]").Replace(
            "\"schemaVersion\": \"2\"",
            "\"schemaVersion\": \"1\"",
            StringComparison.Ordinal)];
        yield return ["unknown kind", MasterJson("unknown", "[]")];
        yield return ["duplicate key and start", MasterJson("basic-rewards", Entries(
            Entry("a", "2024-04", "2024-05"),
            Entry("a", "2024-04", null)))];
        yield return ["reversed range", MasterJson("basic-rewards", Entries(Entry("a", "2024-06", "2024-05")))];
        yield return ["overlap", MasterJson("basic-rewards", Entries(
            Entry("a", "2024-04", "2024-06"),
            Entry("a", "2024-06", null)))];
        yield return ["gap", MasterJson("basic-rewards", Entries(
            Entry("a", "2024-04", "2024-05"),
            Entry("a", "2024-07", null)))];
        yield return ["finite last", MasterJson("basic-rewards", Entries(Entry("a", "2024-04", "2024-05")))];
        yield return ["unknown source", MasterJson("basic-rewards", Entries(Entry("a", "2024-04", null, "missing")))];
        yield return ["empty values", MasterJson("basic-rewards", Entries(Entry("a", "2024-04", null, values: "{}")))];
        yield return ["unknown top-level", MasterJson("basic-rewards", "[]").Replace("\"entries\":", "\"unknown\": true, \"entries\":", StringComparison.Ordinal)];
        yield return ["unknown entry", MasterJson("basic-rewards", Entries(Entry("a", "2024-04", null).Replace("\"values\":", "\"unknown\": true, \"values\":", StringComparison.Ordinal)))];
        yield return ["case mismatch", MasterJson("basic-rewards", "[]").Replace("\"masterKind\"", "\"MasterKind\"", StringComparison.Ordinal)];
        yield return ["duplicate property", MasterJson("basic-rewards", "[]").Replace("\"entries\": []", "\"entries\": [], \"entries\": []", StringComparison.Ordinal)];
    }

    [Theory]
    [MemberData(nameof(EmbeddedReleaseCases))]
    public void LoadEmbedded_resolves_the_release_for_inclusive_service_months(
        int year,
        int month,
        string expectedVersion)
    {
        var provider = JsonClaimMasterProvider.LoadEmbedded();

        provider.ResolveVersion(new ServiceMonth(year, month)).Version.Value
            .Should().Be(expectedVersion);
    }

    [Fact]
    public void Embedded_source_catalog_matches_the_adr_source_ids_and_shape()
    {
        using var stream = OpenEmbedded(".ClaimMasters.Seed.sources.json");
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;

        root.GetProperty("schemaVersion").GetString().Should().Be("1");
        var sourceIds = root.GetProperty("sources").EnumerateArray()
            .Select(source => source.GetProperty("documentId").GetString())
            .ToArray();
        sourceIds.Should().HaveCount(66);
        sourceIds.Should().Equal(ExpectedSourceIds);

        foreach (var source in root.GetProperty("sources").EnumerateArray())
        {
            source.EnumerateObject().Select(property => property.Name).Should().Equal(
                "documentId", "title", "publisher", "effectiveAt", "publishedAt", "retrievedAt",
                "url", "sha256", "supersedes", "corrects", "supplements", "applicabilityNote",
                "correctionNote");
        }
    }

    [Fact]
    public void Embedded_release_bundles_append_the_formula_sources_in_exact_order()
    {
        using var stream = OpenEmbedded(".ClaimMasters.Seed.sources.json");
        using var document = JsonDocument.Parse(stream);
        var releases = document.RootElement.GetProperty("releases").EnumerateArray().ToArray();

        releases.Select(release => release.GetProperty("masterVersion").GetString())
            .Should().Equal(
                "claim-master-r6-04", "claim-master-r6-06", "claim-master-r7-01",
                "claim-master-r7-09", "claim-master-r8-06");
        ReleaseSourceIds(releases[0]).Should().Equal(ExpectedR604Sources);
        ReleaseSourceIds(releases[1]).Should().Equal(ExpectedR606Sources);
        ReleaseSourceIds(releases[2]).Should().Equal(ExpectedR701Sources);
        ReleaseSourceIds(releases[3]).Should().Equal(ExpectedR709Sources);
        ReleaseSourceIds(releases[4]).Should().Equal(ExpectedR806Sources);
        ReleaseSourceIds(releases[4]).Should().NotContain("h31-fee-notice-consolidated");
    }

    [Fact]
    public void Embedded_source_catalog_registers_the_current_fee_notice_html()
    {
        using var stream = OpenEmbedded(".ClaimMasters.Seed.sources.json");
        using var document = JsonDocument.Parse(stream);
        var source = SourceById(document.RootElement, "current-fee-notice-html");

        source.GetProperty("title").GetString().Should().Be(
            "障害者の日常生活及び社会生活を総合的に支援するための法律に基づく指定障害福祉サービス等及び基準該当障害福祉サービスに要する費用の額の算定に関する基準");
        source.GetProperty("publisher").GetString().Should().Be("厚生労働省");
        source.GetProperty("effectiveAt").GetString().Should().Be("2026-06-01");
        source.GetProperty("publishedAt").ValueKind.Should().Be(JsonValueKind.Null);
        source.GetProperty("retrievedAt").GetString().Should().Be("2026-07-14");
        source.GetProperty("url").GetString().Should().Be(
            "https://www.mhlw.go.jp/web/t_doc?dataId=83aa8477&dataType=0&pageNo=6");
        source.GetProperty("sha256").GetString().Should().Be(
            "0b5c75203f589701e8d0d3ba7cf192f4873b7aeae023da6e137882b225286768");
        AssertNullRelationsAndNonblankApplicabilityNote(source);
    }

    [Fact]
    public void Embedded_source_catalog_registers_the_protected_facility_administrative_expense_standard_html()
    {
        using var stream = OpenEmbedded(".ClaimMasters.Seed.sources.json");
        using var document = JsonDocument.Parse(stream);
        var source = SourceById(
            document.RootElement,
            "protected-facility-administrative-expense-standard-html");

        source.GetProperty("title").GetString().Should().Be(
            "生活保護法による保護施設事務費及び委託事務費の支弁基準について");
        source.GetProperty("publisher").GetString().Should().Be("厚生労働省");
        source.GetProperty("effectiveAt").GetString().Should().Be("2023-04-01");
        source.GetProperty("publishedAt").GetString().Should().Be("2023-03-28");
        source.GetProperty("retrievedAt").GetString().Should().Be("2026-07-14");
        source.GetProperty("url").GetString().Should().Be(
            "https://www.mhlw.go.jp/web/t_doc?dataId=00tc7589&dataType=1");
        source.GetProperty("sha256").GetString().Should().Be(
            "e6d94b5279ca33d60daa83f29e6fdb1f5c3d1ba08f076812cf2c0f64a37ba8a5");
        AssertNullRelationsAndNonblankApplicabilityNote(source);
    }

    [Fact]
    public void Embedded_source_catalog_registers_the_H31_fee_notice_consolidated_pdf()
    {
        using var stream = OpenEmbedded(".ClaimMasters.Seed.sources.json");
        using var document = JsonDocument.Parse(stream);
        var source = SourceById(document.RootElement, "h31-fee-notice-consolidated");

        source.GetProperty("title").GetString().Should().Be(
            "障害者の日常生活及び社会生活を総合的に支援するための法律に基づく指定障害福祉サービス等及び基準該当障害福祉サービスに要する費用の額の算定に関する基準等の一部を改正する告示");
        source.GetProperty("publisher").GetString().Should().Be("厚生労働省");
        source.GetProperty("effectiveAt").GetString().Should().Be("2019-10-01");
        source.GetProperty("publishedAt").GetString().Should().Be("2019-03-25");
        source.GetProperty("retrievedAt").GetString().Should().Be("2026-07-14");
        source.GetProperty("url").GetString().Should().Be(
            "https://www.mhlw.go.jp/content/000520560.pdf");
        source.GetProperty("sha256").GetString().Should().Be(
            "79054870b88b1ca97b3b31a811857ed8a614e59da0b6d14435df30bcb5bf4bc9");
        AssertNullRelationsAndNonblankApplicabilityNote(source);
    }

    [Theory]
    [MemberData(nameof(EmbeddedGrantSourceCases))]
    public void Embedded_release_contains_exactly_its_current_grant_decision_source(
        string masterVersion,
        string expectedSourceId)
    {
        using var stream = OpenEmbedded(".ClaimMasters.Seed.sources.json");
        using var document = JsonDocument.Parse(stream);
        var release = document.RootElement.GetProperty("releases").EnumerateArray()
            .Single(item => item.GetProperty("masterVersion").GetString() == masterVersion);

        ReleaseSourceIds(release)
            .Where(sourceId => sourceId.Contains("-grant-decision-administration-", StringComparison.Ordinal))
            .Should().Equal(expectedSourceId);
    }

    [Fact]
    public void Embedded_grant_decision_supersedes_chain_strictly_increases_effective_dates()
    {
        using var stream = OpenEmbedded(".ClaimMasters.Seed.sources.json");
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;
        string[] descendingChain =
        [
            "r8-grant-decision-administration-202606",
            "r7-grant-decision-administration-202509",
            "r7-grant-decision-administration-202501",
            "r6-grant-decision-administration-202404",
        ];

        for (var index = 0; index < descendingChain.Length - 1; index++)
        {
            var source = SourceById(root, descendingChain[index]);
            var superseded = SourceById(root, descendingChain[index + 1]);

            RelationIds(source, "supersedes").Should().Equal(descendingChain[index + 1]);
            DateOnly.ParseExact(
                    source.GetProperty("effectiveAt").GetString()!,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture)
                .Should().BeAfter(DateOnly.ParseExact(
                    superseded.GetProperty("effectiveAt").GetString()!,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture));
        }

        SourceById(root, descendingChain[^1]).GetProperty("supersedes").ValueKind.Should()
            .Be(JsonValueKind.Null);
    }

    [Fact]
    public void Embedded_catalog_registers_the_R6_adult_burden_source_from_the_official_distribution()
    {
        using var stream = OpenEmbedded(".ClaimMasters.Seed.sources.json");
        using var document = JsonDocument.Parse(stream);
        var source = SourceById(document.RootElement, "r6-grant-decision-administration-202404");

        source.GetProperty("title").GetString().Should()
            .Be("介護給付費等に係る支給決定事務等について（事務処理要領・最終改正令和6年4月）");
        source.GetProperty("publisher").GetString().Should().Be("厚生労働省（福島県公式ページ再配布）");
        source.GetProperty("effectiveAt").GetString().Should().Be("2024-04-01");
        source.GetProperty("publishedAt").GetString().Should().Be("2024-03-29");
        source.GetProperty("retrievedAt").GetString().Should().Be("2026-07-11");
        source.GetProperty("url").GetString().Should()
            .Be("https://www.pref.fukushima.lg.jp/uploaded/attachment/624888.pdf");
        source.GetProperty("sha256").GetString().Should()
            .Be("ddbcaf0421f9fad4fcb925515247363ea19eb5880dbb0f4d8b54922dada303c8");
        var note = source.GetProperty("applicabilityNote").GetString();
        note.Should().Contain("福島県公式配布ページ");
        note.Should().Contain("4,587,513 bytes");
        note.Should().Contain("physical pages 170〜183");
        note.Should().Contain("2024-04〜2024-12");
    }

    [Fact]
    public void Embedded_catalog_registers_the_archived_R7_January_adult_burden_source()
    {
        using var stream = OpenEmbedded(".ClaimMasters.Seed.sources.json");
        using var document = JsonDocument.Parse(stream);
        var source = SourceById(document.RootElement, "r7-grant-decision-administration-202501");

        source.GetProperty("title").GetString().Should()
            .Be("介護給付費等に係る支給決定事務等について（事務処理要領・最終改正令和7年1月）");
        source.GetProperty("publisher").GetString().Should().Be("厚生労働省");
        source.GetProperty("effectiveAt").GetString().Should().Be("2025-01-01");
        source.GetProperty("publishedAt").ValueKind.Should().Be(JsonValueKind.Null);
        source.GetProperty("retrievedAt").GetString().Should().Be("2026-07-11");
        source.GetProperty("url").GetString().Should()
            .Be("https://web.archive.org/web/20250317120118id_/https://www.mhlw.go.jp/content/12200000/001242850.pdf");
        source.GetProperty("sha256").GetString().Should()
            .Be("f3667ee8504dd86c39ae9bd35996f67ba03b89115878fa76a05eaa6d181f9f8e");
        RelationIds(source, "supersedes").Should().Equal("r6-grant-decision-administration-202404");
        var note = source.GetProperty("applicabilityNote").GetString();
        note.Should().Contain("https://www.mhlw.go.jp/content/12200000/001242850.pdf");
        note.Should().Contain("404");
        note.Should().Contain("20250317120118id_");
        note.Should().Contain("1,881,376 bytes");
        note.Should().Contain("2025-01〜2025-08");
        source.GetProperty("correctionNote").GetString().Should()
            .Contain("r6-grant-decision-administration-202404");
    }

    [Fact]
    public void Embedded_catalog_registers_the_archived_R7_September_adult_burden_source()
    {
        using var stream = OpenEmbedded(".ClaimMasters.Seed.sources.json");
        using var document = JsonDocument.Parse(stream);
        var source = SourceById(document.RootElement, "r7-grant-decision-administration-202509");

        source.GetProperty("title").GetString().Should()
            .Be("介護給付費等に係る支給決定事務等について（事務処理要領・最終改正令和7年9月）");
        source.GetProperty("publisher").GetString().Should().Be("厚生労働省");
        source.GetProperty("effectiveAt").GetString().Should().Be("2025-09-01");
        source.GetProperty("publishedAt").ValueKind.Should().Be(JsonValueKind.Null);
        source.GetProperty("retrievedAt").GetString().Should().Be("2026-07-11");
        source.GetProperty("url").GetString().Should()
            .Be("https://web.archive.org/web/20251001223141id_/https://www.mhlw.go.jp/content/12200000/001571725.pdf");
        source.GetProperty("sha256").GetString().Should()
            .Be("243686e446eb695468ebe370ddabaed4b7743f5afd9ef60e29afc0019ead97cc");
        RelationIds(source, "supersedes").Should().Equal("r7-grant-decision-administration-202501");
        var note = source.GetProperty("applicabilityNote").GetString();
        note.Should().Contain("https://www.mhlw.go.jp/content/12200000/001571725.pdf");
        note.Should().Contain("404");
        note.Should().Contain("20251001223141id_");
        note.Should().Contain("1,944,918 bytes");
        note.Should().Contain("2025-09〜2026-05");
        source.GetProperty("correctionNote").GetString().Should()
            .Contain("r7-grant-decision-administration-202501");
    }

    [Fact]
    public void Embedded_catalog_links_the_R8_adult_burden_source_to_the_R7_September_source()
    {
        using var stream = OpenEmbedded(".ClaimMasters.Seed.sources.json");
        using var document = JsonDocument.Parse(stream);
        var source = SourceById(document.RootElement, "r8-grant-decision-administration-202606");

        source.GetProperty("publisher").GetString().Should().Be("厚生労働省（北九州市公式サイト再配布）");
        source.GetProperty("effectiveAt").GetString().Should().Be("2026-06-01");
        source.GetProperty("retrievedAt").GetString().Should().Be("2026-07-11");
        source.GetProperty("url").GetString().Should()
            .Be("https://www.city.kitakyushu.lg.jp/files/001215921.pdf");
        source.GetProperty("sha256").GetString().Should()
            .Be("c5070de88b83528860e8dba6c4aa88ec4bd7418dea017fbbdb5cc80dc7014798");
        RelationIds(source, "supersedes").Should().Equal("r7-grant-decision-administration-202509");
        source.GetProperty("correctionNote").GetString().Should().Contain("2026-06");
        source.GetProperty("correctionNote").GetString().Should()
            .Contain("r7-grant-decision-administration-202509");
        var note = source.GetProperty("applicabilityNote").GetString();
        note.Should().Contain("北九州市公式サイト");
        note.Should().Contain("1,968,795 bytes");
        note.Should().Contain("physical pages 173、175、182、184〜186");
        note.Should().Contain("https://www.mhlw.go.jp/content/12200000/001470632.pdf");
        note.Should().Contain("404");
    }

    [Fact]
    public void Load_rejects_the_legacy_open_values_object()
    {
        var masterFiles = ValidMasterJsons();
        masterFiles["basic-rewards.json"] = MasterJson(
            "basic-rewards",
            Entries(Entry("a", "2024-04", null, values: "{\"futureProperty\":42}")));

        var action = () => Load(ValidCatalogJson, masterFiles);

        action.Should().Throw<InvalidDataException>();
    }

    [Theory]
    [MemberData(nameof(InvalidCatalogCases))]
    public void Load_rejects_invalid_or_non_strict_catalog_json(
        string _,
        string json,
        bool expectsArgumentException)
    {
        var action = () => Load(json, ValidMasterJsons());

        if (expectsArgumentException)
            action.Should().Throw<ArgumentException>();
        else
            action.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Load_rejects_a_relation_without_a_correction_note()
    {
        var json = MutateCatalog(root =>
        {
            AddSource(root, "doc-2");
            root["sources"]![0]!["corrects"] = new JsonArray("doc-2");
        });

        var action = () => Load(json, ValidMasterJsons());

        action.Should().Throw<InvalidDataException>()
            .WithMessage("*correctionNote*");
    }

    [Fact]
    public void Load_rejects_a_correction_note_without_any_relation()
    {
        var json = MutateSource(source => source["correctionNote"] = "No relation exists");

        var action = () => Load(json, ValidMasterJsons());

        action.Should().Throw<InvalidDataException>()
            .WithMessage("*correctionNote*");
    }

    [Fact]
    public void Load_rejects_two_distinct_supersedes_targets_via_the_max_one_guard()
    {
        var json = MutateCatalog(root =>
        {
            AddSource(root, "doc-2");
            var source = AddSource(root, "doc-3");
            source["supersedes"] = new JsonArray("doc-1", "doc-2");
            source["correctionNote"] = "doc-1 and doc-2 are both replaced";
        });

        var action = () => Load(json, ValidMasterJsons());

        action.Should().Throw<InvalidDataException>()
            .WithMessage("*more than one supersedes target*");
    }

    [Fact]
    public void Load_wraps_catalog_json_errors_with_the_resource_name()
    {
        var json = ValidCatalogJson.Replace("\"documentId\"", "\"DocumentId\"", StringComparison.Ordinal);

        var action = () => Load(json, ValidMasterJsons());

        action.Should().Throw<InvalidDataException>()
            .WithMessage("*sources.json*")
            .WithInnerException<JsonException>();
    }

    [Theory]
    [MemberData(nameof(FileKindMappings))]
    public void Load_rejects_each_filename_kind_mismatch(string fileName, string expectedKind)
    {
        var masters = ValidMasterJsons();
        masters[fileName] = MasterJson(
            string.Equals(expectedKind, "basic-rewards", StringComparison.Ordinal)
                ? "additions"
                : "basic-rewards",
            "[]");

        var action = () => Load(ValidCatalogJson, masters);

        action.Should().Throw<InvalidDataException>();
    }

    [Theory]
    [MemberData(nameof(FileKindMappings))]
    public void Load_rejects_each_missing_master_file(string fileName, string _)
    {
        var masters = ValidMasterJsons();
        masters.Remove(fileName);

        var action = () => Load(ValidCatalogJson, masters);

        action.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Load_rejects_an_extra_master_file()
    {
        var masters = ValidMasterJsons();
        masters["extra.json"] = MasterJson("basic-rewards", "[]");

        var action = () => Load(ValidCatalogJson, masters);

        action.Should().Throw<InvalidDataException>();
    }

    [Theory]
    [MemberData(nameof(InvalidMasterCases))]
    public void Load_rejects_invalid_or_non_strict_master_json(string _, string json)
    {
        var masters = ValidMasterJsons();
        masters["basic-rewards.json"] = json;

        var action = () => Load(ValidCatalogJson, masters);

        action.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Load_wraps_master_json_errors_with_the_filename()
    {
        var masters = ValidMasterJsons();
        masters["basic-rewards.json"] = "{\"schemaVersion\":\"2\"";

        var action = () => Load(ValidCatalogJson, masters);

        action.Should().Throw<InvalidDataException>()
            .WithMessage("*basic-rewards.json*")
            .WithInnerException<JsonException>();
    }

    [Fact]
    public void Load_reports_master_structure_errors_before_catalog_json_errors()
    {
        var masters = ValidMasterJsons();
        masters["basic-rewards.json"] = MasterJson("basic-rewards", "[]")
            .Replace("\"entries\":", "\"unknown\": true, \"entries\":", StringComparison.Ordinal);

        var action = () => Load("{\"schemaVersion\":", masters);

        action.Should().Throw<InvalidDataException>()
            .WithMessage("*basic-rewards.json*unknown*");
    }

    [Fact]
    public void Load_rejects_null_inputs_and_null_master_streams()
    {
        using var sources = StreamOf(ValidCatalogJson);
        var validStreams = ValidMasterJsons().ToDictionary(
            pair => pair.Key,
            pair => (Stream)StreamOf(pair.Value),
            StringComparer.Ordinal);
        try
        {
            var nullSource = () => JsonClaimMasterProvider.Load(null!, validStreams);
            var nullMasters = () => JsonClaimMasterProvider.Load(sources, null!);
            var withNullStream = new Dictionary<string, Stream>(validStreams, StringComparer.Ordinal)
            {
                ["basic-rewards.json"] = null!,
            };
            var nullStream = () => JsonClaimMasterProvider.Load(sources, withNullStream);

            nullSource.Should().Throw<ArgumentNullException>();
            nullMasters.Should().Throw<ArgumentNullException>();
            nullStream.Should().Throw<ArgumentException>();
        }
        finally
        {
            foreach (var stream in validStreams.Values)
                stream.Dispose();
        }
    }

    [Fact]
    public void Embedded_schema_resources_express_the_runtime_contract()
    {
        using var sourceSchemaStream = OpenEmbedded(".ClaimMasters.Schema.source-catalog.schema.json");
        using var masterSchemaStream = OpenEmbedded(".ClaimMasters.Schema.claim-master-file.schema.json");
        using var sourceSchema = JsonDocument.Parse(sourceSchemaStream);
        using var masterSchema = JsonDocument.Parse(masterSchemaStream);

        var sourceRoot = sourceSchema.RootElement;
        sourceRoot.GetProperty("$schema").GetString().Should().Contain("2020-12");
        sourceRoot.GetProperty("additionalProperties").GetBoolean().Should().BeFalse();
        Required(sourceRoot).Should().BeEquivalentTo(["schemaVersion", "sources", "releases"]);
        var source = sourceRoot.GetProperty("$defs").GetProperty("source");
        source.GetProperty("additionalProperties").GetBoolean().Should().BeFalse();
        Required(source).Should().Contain(
            "documentId", "title", "publisher", "effectiveAt", "publishedAt", "retrievedAt",
            "url", "sha256", "supersedes", "corrects", "supplements", "applicabilityNote",
            "correctionNote");
        Types(source.GetProperty("properties").GetProperty("publishedAt")).Should().Contain("null");
        Types(source.GetProperty("properties").GetProperty("supersedes")).Should().Contain("null");
        var release = sourceRoot.GetProperty("$defs").GetProperty("release");
        release.GetProperty("additionalProperties").GetBoolean().Should().BeFalse();
        Types(release.GetProperty("properties").GetProperty("effectiveTo")).Should().Contain("null");

        var masterRoot = masterSchema.RootElement;
        masterRoot.GetProperty("$schema").GetString().Should().Contain("2020-12");
        masterRoot.GetProperty("additionalProperties").GetBoolean().Should().BeFalse();
        Required(masterRoot).Should().BeEquivalentTo(["schemaVersion", "masterKind", "entries"]);
        masterRoot.GetProperty("properties").GetProperty("schemaVersion")
            .GetProperty("const").GetString().Should().Be("2");
        masterRoot.GetProperty("properties").GetProperty("masterKind").GetProperty("enum")
            .EnumerateArray().Select(item => item.GetString()).Should().Equal(
                "basic-rewards", "additions", "region-unit-prices", "burden-caps",
                "transition-rules", "service-codes");
        var entry = masterRoot.GetProperty("$defs").GetProperty("entry");
        entry.GetProperty("additionalProperties").GetBoolean().Should().BeFalse();
        Required(entry).Should().BeEquivalentTo(
            [
                "key", "effectiveFrom", "effectiveTo", "sourceRefs", "values",
            ]);
        Types(entry.GetProperty("properties").GetProperty("effectiveTo")).Should().Contain("null");
        var sourceRef = masterRoot.GetProperty("$defs").GetProperty("sourceRef");
        sourceRef.GetProperty("additionalProperties").GetBoolean().Should().BeFalse();
        Required(sourceRef).Should().BeEquivalentTo(
            ["documentId", "sha256", "locator", "evidenceRole", "supports"]);
        sourceRef.GetProperty("properties").GetProperty("supports").GetProperty("minItems")
            .GetInt32().Should().Be(1);
        masterRoot.GetProperty("$defs").GetProperty("unitAdjustmentAmount")
            .GetProperty("oneOf").GetArrayLength().Should().Be(4);
        var percentageAmount = masterRoot.GetProperty("$defs")
            .GetProperty("percentageOfTargetAmount");
        Required(percentageAmount).Should().Contain("applicationKind");
        percentageAmount.GetProperty("properties").GetProperty("applicationKind")
            .GetProperty("enum").EnumerateArray().Select(item => item.GetString())
            .Should().Equal("add", "subtract", "replace");
        var proratedAmount = masterRoot.GetProperty("$defs")
            .GetProperty("proratedUnitsAmount");
        Required(proratedAmount).Should().NotContain("maximumRecipientsPerStaff");
        var maximumRecipientsPerStaff = proratedAmount.GetProperty("properties")
            .GetProperty("maximumRecipientsPerStaff");
        maximumRecipientsPerStaff.GetProperty("type").GetString().Should().Be("integer");
        maximumRecipientsPerStaff.GetProperty("minimum").GetInt32().Should().Be(1);
        masterRoot.GetProperty("$defs").GetProperty("serviceCodeUnitRule")
            .GetProperty("oneOf").GetArrayLength().Should().Be(3);
        var definitions = masterRoot.GetProperty("$defs");
        definitions.GetProperty("sourceSupport").GetProperty("enum")
            .EnumerateArray().Select(item => item.GetString()).Should().Equal(
                "service-identity",
                "selectors",
                "unit-rule-kind",
                "unit-rule-value",
                "unit-rule-target",
                "unit-rule-step",
                "unit-rule-rounding",
                "conditions",
                "effective-period",
                "master-values",
                "unit-rule-formula",
                "unit-rule-comparison",
                "unit-rule-local-government-adjustment",
                "unit-rule-runtime-input",
                "unit-rule-runtime-input-provenance");
        var formulaRule = definitions.GetProperty("formulaRule");
        formulaRule.GetProperty("oneOf").GetArrayLength().Should().Be(3);

        var runtimeInputRequirement = definitions
            .GetProperty("protectedFacilityAdministrativeExpenseRequirement");
        Required(runtimeInputRequirement).Should().Equal(
            "key", "valueKind", "valueUnit", "scope", "asOfPolicy", "provenancePolicyId");
        PropertyNames(runtimeInputRequirement).Should().Equal(
            "key", "valueKind", "valueUnit", "scope", "asOfPolicy", "provenancePolicyId");
        runtimeInputRequirement.GetProperty("additionalProperties").GetBoolean()
            .Should().BeFalse();
        var runtimeInputProperties = runtimeInputRequirement.GetProperty("properties");
        runtimeInputProperties.GetProperty("key").GetProperty("const").GetString()
            .Should().Be("protected-facility-administrative-expense-yen");
        runtimeInputProperties.GetProperty("valueKind").GetProperty("const").GetString()
            .Should().Be("entered-yen");
        runtimeInputProperties.GetProperty("valueUnit").GetProperty("const").GetString()
            .Should().Be("yen-per-person-per-month");
        runtimeInputProperties.GetProperty("scope").GetProperty("const").GetString()
            .Should().Be("facility-and-service-fiscal-year");
        runtimeInputProperties.GetProperty("asOfPolicy").GetProperty("const").GetString()
            .Should().Be("service-fiscal-year-april-first");
        runtimeInputProperties.GetProperty("provenancePolicyId").GetProperty("const")
            .GetString().Should()
            .Be("claim.input.protected-facility-administrative-expense.v1");

        var statutoryFormula = definitions.GetProperty("protectedFacilityStatutoryFormula");
        Required(statutoryFormula).Should().Equal(
            "daysDivisor",
            "expenseAdjustmentDivisor",
            "unitPriceDivisorYen",
            "fixedAdditionUnits",
            "upliftRate",
            "calculationStepId",
            "roundingRuleId");
        PropertyNames(statutoryFormula).Should().Equal(
            "daysDivisor",
            "expenseAdjustmentDivisor",
            "unitPriceDivisorYen",
            "fixedAdditionUnits",
            "upliftRate",
            "calculationStepId",
            "roundingRuleId");
        statutoryFormula.GetProperty("additionalProperties").GetBoolean().Should().BeFalse();
        var statutoryFormulaProperties = statutoryFormula.GetProperty("properties");
        statutoryFormulaProperties.GetProperty("daysDivisor").GetProperty("const")
            .GetInt32().Should().Be(22);
        statutoryFormulaProperties.GetProperty("expenseAdjustmentDivisor").GetProperty("const")
            .GetString().Should().Be("0.945");
        statutoryFormulaProperties.GetProperty("unitPriceDivisorYen").GetProperty("const")
            .GetInt32().Should().Be(10);
        statutoryFormulaProperties.GetProperty("fixedAdditionUnits").GetProperty("const")
            .GetInt32().Should().Be(23);
        statutoryFormulaProperties.GetProperty("upliftRate").GetProperty("const")
            .GetString().Should().Be("1.046");
        statutoryFormulaProperties.GetProperty("calculationStepId").GetProperty("const")
            .GetString().Should()
            .Be("claim.step.units.service-code.protected-facility-formula.v1");
        statutoryFormulaProperties.GetProperty("roundingRuleId").GetProperty("const")
            .GetString().Should().Be("claim.rounding.units.half-up.v1");

        var localGovernmentAdjustment = definitions
            .GetProperty("protectedFacilityLocalGovernmentAdjustment");
        Required(localGovernmentAdjustment).Should().Equal(
            "conditionSelector", "rate", "target", "calculationStepId", "roundingRuleId");
        PropertyNames(localGovernmentAdjustment).Should().Equal(
            "conditionSelector", "rate", "target", "calculationStepId", "roundingRuleId");
        localGovernmentAdjustment.GetProperty("additionalProperties").GetBoolean()
            .Should().BeFalse();
        var localGovernmentAdjustmentProperties = localGovernmentAdjustment
            .GetProperty("properties");
        localGovernmentAdjustmentProperties.GetProperty("conditionSelector")
            .GetProperty("const").GetString().Should()
            .Be("municipality-ownership:local-government");
        localGovernmentAdjustmentProperties.GetProperty("rate").GetProperty("const")
            .GetString().Should().Be("0.965");
        localGovernmentAdjustmentProperties.GetProperty("target").GetProperty("const")
            .GetString().Should().Be("comparison-only");
        localGovernmentAdjustmentProperties.GetProperty("calculationStepId")
            .GetProperty("const").GetString().Should()
            .Be("claim.step.units.service-code.protected-facility-local-government-benchmark.v1");
        localGovernmentAdjustmentProperties.GetProperty("roundingRuleId").GetProperty("const")
            .GetString().Should().Be("claim.rounding.units.half-up.v1");

        var benchmark = definitions.GetProperty("protectedFacilityBenchmark");
        Required(benchmark).Should().Equal(
            "officialSection",
            "basicRewardStaffingKey",
            "paymentBandMatch",
            "capacityMatch",
            "localGovernmentAdjustment");
        PropertyNames(benchmark).Should().Equal(
            "officialSection",
            "basicRewardStaffingKey",
            "paymentBandMatch",
            "capacityMatch",
            "localGovernmentAdjustment");
        benchmark.GetProperty("additionalProperties").GetBoolean().Should().BeFalse();
        var benchmarkProperties = benchmark.GetProperty("properties");
        benchmarkProperties.GetProperty("officialSection").GetProperty("const")
            .GetString().Should().Be("b-type-service-fee-ii");
        benchmarkProperties.GetProperty("basicRewardStaffingKey").GetProperty("const")
            .GetString().Should().Be("b-type-service-fee-ii");
        benchmarkProperties.GetProperty("paymentBandMatch").GetProperty("const")
            .GetString().Should().Be("same-average-wage-band");
        benchmarkProperties.GetProperty("capacityMatch").GetProperty("const")
            .GetString().Should().Be("same-capacity-band");
        benchmarkProperties.GetProperty("localGovernmentAdjustment").GetProperty("$ref")
            .GetString().Should().Be("#/$defs/protectedFacilityLocalGovernmentAdjustment");

        var selection = definitions.GetProperty("protectedFacilityMinimumSelection");
        Required(selection).Should().Equal("kind", "calculationStepId", "roundingRuleId");
        PropertyNames(selection).Should().Equal("kind", "calculationStepId", "roundingRuleId");
        selection.GetProperty("additionalProperties").GetBoolean().Should().BeFalse();
        var selectionProperties = selection.GetProperty("properties");
        selectionProperties.GetProperty("kind").GetProperty("const").GetString()
            .Should().Be("minimum");
        selectionProperties.GetProperty("calculationStepId").GetProperty("const")
            .GetString().Should()
            .Be("claim.step.units.service-code.protected-facility-minimum.v1");
        selectionProperties.GetProperty("roundingRuleId").GetProperty("const").ValueKind
            .Should().Be(JsonValueKind.Null);

        var protectedRule = definitions.GetProperty("protectedFacilityBenchmarkMinimumRule");
        Required(protectedRule).Should().Equal(
            "kind",
            "mode",
            "runtimeInputRequirement",
            "statutoryFormula",
            "benchmark",
            "selection",
            "factors",
            "billingUnit");
        PropertyNames(protectedRule).Should().Equal(
            "kind",
            "mode",
            "runtimeInputRequirement",
            "statutoryFormula",
            "benchmark",
            "selection",
            "factors",
            "billingUnit");
        protectedRule.GetProperty("additionalProperties").GetBoolean().Should().BeFalse();
        var protectedRuleProperties = protectedRule.GetProperty("properties");
        protectedRuleProperties.GetProperty("kind").GetProperty("const").GetString()
            .Should().Be("formula");
        protectedRuleProperties.GetProperty("mode").GetProperty("const").GetString()
            .Should().Be("protected-facility-benchmark-minimum");
        protectedRuleProperties.GetProperty("runtimeInputRequirement").GetProperty("$ref")
            .GetString().Should()
            .Be("#/$defs/protectedFacilityAdministrativeExpenseRequirement");
        protectedRuleProperties.GetProperty("statutoryFormula").GetProperty("$ref")
            .GetString().Should().Be("#/$defs/protectedFacilityStatutoryFormula");
        protectedRuleProperties.GetProperty("benchmark").GetProperty("$ref")
            .GetString().Should().Be("#/$defs/protectedFacilityBenchmark");
        protectedRuleProperties.GetProperty("selection").GetProperty("$ref")
            .GetString().Should().Be("#/$defs/protectedFacilityMinimumSelection");
        var protectedRuleFactors = protectedRuleProperties.GetProperty("factors");
        protectedRuleFactors.GetProperty("type").GetString().Should().Be("array");
        protectedRuleFactors.GetProperty("items").GetProperty("$ref").GetString()
            .Should().Be("#/$defs/formulaFactor");
        protectedRuleFactors.TryGetProperty("minItems", out _).Should().BeFalse();
        protectedRuleProperties.GetProperty("billingUnit").GetProperty("const").GetString()
            .Should().Be("per-day");
        var conditionDefinition = definitions.GetProperty("conditionDefinition");
        var r8ConditionRule = conditionDefinition.GetProperty("allOf")
            .EnumerateArray()
            .Single(rule => rule.TryGetProperty("if", out var condition)
                && condition.TryGetProperty("properties", out var properties)
                && properties.TryGetProperty("kind", out var kindProperty)
                && kindProperty.TryGetProperty("const", out var kind)
                && kind.GetString() == "r8-reform-status");
        var r8Values = r8ConditionRule.GetProperty("then").GetProperty("properties");
        r8Values.GetProperty("value").GetProperty("enum").EnumerateArray()
            .Select(item => item.GetString()).Should().Equal(
                "not-applicable-before-r8",
                "reform-target",
                "reform-exempt",
                "unchanged-below-15000");
        r8Values.GetProperty("values").GetProperty("items").GetProperty("enum")
            .EnumerateArray().Select(item => item.GetString()).Should().Equal(
                "not-applicable-before-r8",
                "reform-target",
                "reform-exempt",
                "unchanged-below-15000");
        definitions.TryGetProperty("componentRef", out _).Should().BeTrue();
    }

    private static JsonClaimMasterProvider Load(
        string catalogJson,
        IReadOnlyDictionary<string, string> masterJsons)
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

    private static Dictionary<string, string> ValidMasterJsons() => FileKindMappings
        .ToDictionary(
            item => item[0].ToString()!,
            item => MasterJson(item[1].ToString()!, "[]"),
            StringComparer.Ordinal);

    private static MemoryStream StreamOf(string json) => new(Encoding.UTF8.GetBytes(json));

    private static Stream OpenEmbedded(string suffix)
    {
        var assembly = typeof(JsonClaimMasterProvider).Assembly;
        var names = assembly.GetManifestResourceNames()
            .Where(name => name.EndsWith(suffix, StringComparison.Ordinal))
            .ToArray();
        names.Should().ContainSingle();
        return assembly.GetManifestResourceStream(names[0])!;
    }

    private static string MutateCatalog(Action<JsonObject> mutate)
    {
        var root = JsonNode.Parse(ValidCatalogJson)!.AsObject();
        mutate(root);
        return root.ToJsonString();
    }

    private static string MutateSource(Action<JsonObject> mutate) => MutateCatalog(root =>
        mutate(root["sources"]![0]!.AsObject()));

    private static JsonObject AddSource(JsonObject root, string documentId)
    {
        var sources = root["sources"]!.AsArray();
        var source = sources[0]!.DeepClone().AsObject();
        source["documentId"] = documentId;
        source["url"] = $"https://example.test/{documentId}.pdf";
        sources.Add(source);
        return source;
    }

    private static string[] ReleaseSourceIds(JsonElement release) => release
        .GetProperty("sourceDocumentIds")
        .EnumerateArray()
        .Select(item => item.GetString()!)
        .ToArray();

    private static JsonElement SourceById(JsonElement root, string documentId) => root
        .GetProperty("sources")
        .EnumerateArray()
        .Single(source => source.GetProperty("documentId").GetString() == documentId);

    private static string[] RelationIds(JsonElement source, string relation) => source
        .GetProperty(relation)
        .EnumerateArray()
        .Select(item => item.GetString()!)
        .ToArray();

    private static void AssertNullRelationsAndNonblankApplicabilityNote(JsonElement source)
    {
        source.GetProperty("supersedes").ValueKind.Should().Be(JsonValueKind.Null);
        source.GetProperty("corrects").ValueKind.Should().Be(JsonValueKind.Null);
        source.GetProperty("supplements").ValueKind.Should().Be(JsonValueKind.Null);
        source.GetProperty("correctionNote").ValueKind.Should().Be(JsonValueKind.Null);
        source.GetProperty("applicabilityNote").GetString().Should().NotBeNullOrWhiteSpace();
    }

    private static string[] Required(JsonElement schema) => schema.GetProperty("required")
        .EnumerateArray().Select(item => item.GetString()!).ToArray();

    private static string[] PropertyNames(JsonElement schema) => schema.GetProperty("properties")
        .EnumerateObject().Select(property => property.Name).ToArray();

    private static string[] Types(JsonElement schema) => schema.GetProperty("type").ValueKind switch
    {
        JsonValueKind.String => [schema.GetProperty("type").GetString()!],
        JsonValueKind.Array => schema.GetProperty("type").EnumerateArray()
            .Select(item => item.GetString()!).ToArray(),
        _ => throw new InvalidOperationException("Schema type must be a string or array."),
    };

    private static string MasterJson(string kind, string entries)
    {
        var conditions = string.Equals(kind, "service-codes", StringComparison.Ordinal)
            ? "  \"conditionDefinitions\": [],\n"
            : string.Empty;
        return
            "{\n" +
            "  \"schemaVersion\": \"2\",\n" +
            "  \"masterKind\": \"" + kind + "\",\n" +
            conditions +
            "  \"entries\": " + entries + "\n" +
            "}\n";
    }

    private static string Entries(params string[] entries) => $"[{string.Join(',', entries)}]";

    private static string Entry(
        string key,
        string effectiveFrom,
        string? effectiveTo,
        string sourceDocumentId = "doc-1",
        string values = "{\"paymentBand\":\"band-1\",\"staffingKey\":\"staffing-1\",\"capacityKey\":\"capacity-1\",\"serviceCode\":\"100001\",\"baseUnits\":100}") => $$"""
        {
          "key": "{{key}}",
          "effectiveFrom": "{{effectiveFrom}}",
          "effectiveTo": {{(effectiveTo is null ? "null" : $"\"{effectiveTo}\"")}},
          "sourceRefs": [
            {
              "documentId": "{{sourceDocumentId}}",
              "sha256": "{{Sha256}}",
              "locator": "source:{{sourceDocumentId}}",
              "evidenceRole": "authoritative",
              "supports": ["master-values", "effective-period"]
            }
          ],
          "values": {{values}}
        }
        """;

    private const string ValidCatalogJson = """
        {
          "schemaVersion": "1",
          "sources": [
            {
              "documentId": "doc-1",
              "title": "Official source",
              "publisher": "Ministry",
              "effectiveAt": "2024-04-01",
              "publishedAt": null,
              "retrievedAt": "2026-07-10",
              "url": "https://example.test/source.pdf",
              "sha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
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
              "effectiveFrom": "2024-04",
              "effectiveTo": null,
              "sourceDocumentIds": ["doc-1"]
            }
          ]
        }
        """;

    private static readonly string[] ExpectedR604Sources =
    [
        "mhlw-unit-price-notice-observed-946c3d96", "r6-revision-overview", "r6-fee-notice",
        "r6-calculation-note", "r6-employment-guidance-r6", "r6-employment-guidance",
        "r6-employment-guidance-corr-4", "r6-qa-v1", "r6-qa-v2", "r6-qa-v3", "r6-qa-v5",
        "r6-qa-v6", "r6-qa-v7", "r6-qa-v8", "r6-calculation-corr-1", "r6-calculation-corr-3",
        "r6-calculation-corr-5", "r6-calculation-corr-6", "r6-calculation-corr-7",
        "r6-calculation-corr-8", "r6-qa-corr-1", "r6-qa-corr-2", "r6-qa-corr-3",
        "r6-capability-202404", "r6-reward-structure", "r6-service-codes-2-pdf",
        "r6-service-codes-2-xlsx", "r6-claim-decision-202404-pdf", "r6-claim-decision-202404-xls",
        "r6-disability-support-guide-202404", "r6-claim-handbook-202405",
        "r6-grant-decision-administration-202404",
        "current-fee-notice-html", "protected-facility-administrative-expense-standard-html",
        "h31-fee-notice-consolidated",
    ];

    private static readonly string[] ExpectedR606Sources =
    [
        "mhlw-unit-price-notice-observed-946c3d96", "r6-revision-overview", "r6-fee-notice",
        "r6-calculation-note", "r6-employment-guidance-r6", "r6-employment-guidance",
        "r6-employment-guidance-corr-4", "r6-qa-v1", "r6-qa-v2", "r6-qa-v3", "r6-qa-v5",
        "r6-qa-v6", "r6-qa-v7", "r6-qa-v8", "r6-calculation-corr-1", "r6-calculation-corr-3",
        "r6-calculation-corr-5", "r6-calculation-corr-6", "r6-calculation-corr-7",
        "r6-calculation-corr-8", "r6-qa-corr-1", "r6-qa-corr-2", "r6-qa-corr-3",
        "r6-capability-202406", "r6-reward-structure", "r6-service-codes-2-pdf",
        "r6-service-codes-2-xlsx", "r6-claim-decision-202406-pdf", "r6-claim-decision-202406-xls",
        "r6-disability-support-guide-202404", "r6-grant-decision-administration-202404",
        "current-fee-notice-html", "protected-facility-administrative-expense-standard-html",
        "h31-fee-notice-consolidated",
    ];

    private static readonly string[] ExpectedR701Sources =
    [
        .. ExpectedR606Sources[..^4],
        "r7-grant-decision-administration-202501",
        "current-fee-notice-html", "protected-facility-administrative-expense-standard-html",
        "h31-fee-notice-consolidated",
    ];

    private static readonly string[] ExpectedR709Sources =
    [
        .. ExpectedR606Sources[..^4],
        "r7-grant-decision-administration-202509",
        "current-fee-notice-html", "protected-facility-administrative-expense-standard-html",
        "h31-fee-notice-consolidated",
    ];

    private static readonly string[] ExpectedR806Sources =
    [
        "mhlw-unit-price-notice-observed-946c3d96", "r6-revision-overview", "r8-revision-overview",
        "r8-fee-notice", "r8-calculation-note", "r8-b-reward-band-guide",
        "r8-employment-transition-guide", "r8-qa-v1", "r8-amendment-qa", "r8-capability-202606",
        "r8-capability-correction", "r8-reward-structure", "r8-service-codes-2-pdf",
        "r8-service-codes-2-xlsx", "r8-claim-decision-pdf", "r8-claim-decision-xls",
        "r6-disability-support-guide-202404", "r8-grant-decision-administration-202606",
        "current-fee-notice-html", "protected-facility-administrative-expense-standard-html",
    ];
}
