using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Tsumugi.Infrastructure.Csv.Specifications;

namespace Tsumugi.Infrastructure.Csv.Tests.Specifications;

/// <summary>
/// 行単位の出典（証跡台帳 <c>spec-evidence-r7-10.json</c>）が、登録済み一次資料と機械的に噛み合って
/// いることを固定する。
/// </summary>
/// <remarks>
/// 狙いは「一次資料が差し替わったときに、<b>どの判断の根拠を再検証すべきか</b>を機械が名指しする」こと。
/// <c>sources.json</c> の <c>liveCheck</c> は「文書が変わった」までしか分からない。
/// </remarks>
public sealed partial class SpecEvidenceLedgerTests
{
    private static readonly Assembly CsvAssembly = typeof(CsvSpecificationCatalog).Assembly;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private static readonly CsvSpecificationCatalog Catalog = CsvSpecificationLoader.LoadEmbedded();

    [Fact]
    public void The_ledger_is_loaded_by_production_code()
    {
        // 台帳の検証は本番の loader（CsvSpecificationCatalog）が行う。テストだけで検証すると
        // 「テストを消せば通る」状態になる。
        Catalog.EvidenceClaims.Should().NotBeEmpty();
        Catalog.EvidenceClaims.Select(claim => claim.ClaimId).Should().OnlyHaveUniqueItems();
        Catalog.EvidenceClaims.Should().OnlyContain(claim => claim.SourceRefs.Count > 0);
    }

    // NOTE(teeth): 一次資料を差し替えて sources.json の SHA-256 を更新すると、その文書に依拠する
    // claim が「根拠を再検証せよ」として落ちる。ここが台帳の存在意義そのもの。
    [Fact]
    public void A_changed_source_document_names_the_claims_that_must_be_re_verified()
    {
        var affected = Catalog.EvidenceClaims
            .Where(claim => claim.SourceRefs.Any(
                sourceRef => string.Equals(sourceRef.DocumentId, "common-r7-10", StringComparison.Ordinal)))
            .Select(claim => claim.ClaimId)
            .ToArray();
        affected.Should().NotBeEmpty("共通編に依拠する判断が台帳に載っていること");

        var act = () => LoadWithRotatedSha("common-r7-10");

        var message = act.Should().Throw<InvalidDataException>().Which.Message;
        message.Should().Contain("common-r7-10").And.Contain("Re-verify");
        // 例外は最初に見つかった claim を名指しする（どこから調べればよいかが分かる）。
        affected.Should().Contain(claimId => message.Contains(claimId, StringComparison.Ordinal));
    }

    [Fact]
    public void Every_citation_that_points_at_an_item_row_matches_the_mechanical_extraction()
    {
        // p.N;item=M 形式の locator は ADR 0037 の抽出結果と突合できる。手書きの頁番号のずれを落とす。
        var extraction = LoadExtraction();
        var pages = extraction.Records
            .SelectMany(record => record.Items.Select(item => (record.RecordId, item.Position, item.SourcePage)))
            .ToDictionary(entry => (entry.RecordId, entry.Position), entry => entry.SourcePage);

        var checked_ = 0;
        foreach (var claim in Catalog.EvidenceClaims)
        {
            foreach (var sourceRef in claim.SourceRefs)
            {
                if (!string.Equals(sourceRef.DocumentId, "provider-r7-10", StringComparison.Ordinal))
                {
                    continue;
                }

                var match = ItemLocator().Match(sourceRef.Locator);
                if (!match.Success)
                {
                    continue;
                }

                var page = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                var position = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                var recordId = string.Equals(claim.ClaimKind, "record", StringComparison.Ordinal)
                    ? claim.ClaimId
                    : RecordIdOf(claim.ClaimId);
                pages.Should().ContainKey((recordId, position), because: claim.ClaimId);
                pages[(recordId, position)].Should().Be(
                    page,
                    because: $"{claim.ClaimId} の locator '{sourceRef.Locator}' は抽出結果の頁と一致すべき");
                checked_++;
            }
        }

        checked_.Should().BeGreaterThan(0, "項目行を指す出典が 1 件以上あること");
    }

    [Fact]
    public void Every_generator_rule_anchor_matches_the_mechanical_extraction()
    {
        // generatorRule に埋め込まれた source=doc:pNN:itemNN アンカー（372 件）を抽出結果と突合する。
        // 手で書いた出典を全件検証できるのでコストゼロで効く。
        var extraction = LoadExtraction();
        var pages = extraction.Records
            .SelectMany(record => record.Items.Select(item => (record.RecordId, item.Position, item.SourcePage)))
            .ToDictionary(entry => (entry.RecordId, entry.Position), entry => entry.SourcePage);

        var failures = new List<string>();
        var verified = 0;
        var unverifiable = 0;
        foreach (var mapping in Catalog.MappingByFieldId.Values.Where(item => item.GeneratorRule is not null))
        {
            foreach (var anchor in RuleAnchor().Matches(mapping.GeneratorRule!).Select(match => match.Groups[1].Value))
            {
                var parts = ItemAnchor().Match(anchor);
                if (!parts.Success)
                {
                    failures.Add($"{mapping.FieldId}: 解析できない source アンカー '{anchor}'");
                    continue;
                }

                var documentId = parts.Groups[1].Value;
                Catalog.SourcesById.Should().ContainKey(documentId, because: mapping.FieldId);
                if (!string.Equals(documentId, "provider-r7-10", StringComparison.Ordinal))
                {
                    // 共通編は 1 頁に 3 表が並ぶため機械抽出が未対応（ADR 0037 の既知ギャップ）。
                    documentId.Should().Be("common-r7-10", because: mapping.FieldId);
                    unverifiable++;
                    continue;
                }

                var page = int.Parse(parts.Groups[2].Value, CultureInfo.InvariantCulture);
                var position = int.Parse(parts.Groups[3].Value, CultureInfo.InvariantCulture);
                var key = (RecordIdOf(mapping.FieldId), position);
                if (!pages.TryGetValue(key, out var extracted))
                {
                    failures.Add($"{mapping.FieldId}: 抽出結果に項番 {position} が無い");
                    continue;
                }

                if (extracted != page)
                {
                    failures.Add($"{mapping.FieldId}: アンカーは p{page} だが抽出結果は p{extracted}");
                    continue;
                }

                verified++;
            }
        }

        failures.Should().BeEmpty(string.Join(Environment.NewLine, failures));
        verified.Should().BeGreaterThan(300, "事業所編のアンカーはほぼ全件検証できる");
        unverifiable.Should().Be(17, "共通編の外側レコード分だけが未検証（ADR 0037 の既知ギャップ）");
    }

    [Fact]
    public void Known_evidence_gaps_are_declared_and_only_shrink()
    {
        // 出典が無い対象は隠さずに宣言する。件数の上限を固定し、増やすときは意図的な判断を強いる。
        Catalog.EvidenceGaps.Should().OnlyContain(
            gap => gap.Reason.Length > 0 && gap.TrackedIn.Length > 0);
        Catalog.EvidenceGaps.Should().HaveCountLessThanOrEqualTo(3);
    }

    /// <summary>
    /// 同一 documentId が別ファイルを指していることが判明している対象。
    /// <c>r8-grant-decision-administration-202606</c> は CSV 側が厚労省の PDF（現在 404・historical として
    /// バイト列を保持）、claim-master 側が北九州市のミラーを指しており、**同じ ID で別ファイルを検証している**。
    /// どちらを正本にするか（または ID を分けるか）は `docs/open-questions.md` で追跡する。
    /// </summary>
    private static readonly string[] KnownDivergentDocumentIds = ["r8-grant-decision-administration-202606"];

    [Fact]
    public void Documents_registered_in_both_registries_agree()
    {
        // 同じ一次資料が claim-master 側と CSV 側の両方に登録されている。SHA-256 と URL が
        // 食い違うと「どちらの版で検証したのか」が不明になる。
        var claimMaster = ReadClaimMasterSources();
        var shared = Catalog.SourcesById.Keys
            .Where(claimMaster.ContainsKey)
            .Where(documentId => !KnownDivergentDocumentIds.Contains(documentId, StringComparer.Ordinal))
            .ToArray();

        shared.Should().NotBeEmpty();
        foreach (var documentId in shared)
        {
            var (sha256, url) = claimMaster[documentId];
            Catalog.SourcesById[documentId].Sha256.Should().Be(sha256, because: documentId);
            Catalog.SourcesById[documentId].Url.Should().Be(url, because: documentId);
        }
    }

    // NOTE(teeth): 既知の相違が解消されたら（ID を分ける／正本を揃える）ここが RED になり、
    // 許容リストから外すことを強制する。
    [Fact]
    public void The_known_registry_divergence_still_exists_and_stays_declared()
    {
        var claimMaster = ReadClaimMasterSources();
        foreach (var documentId in KnownDivergentDocumentIds)
        {
            claimMaster.Should().ContainKey(documentId);
            Catalog.SourcesById.Should().ContainKey(documentId);
            Catalog.SourcesById[documentId].Sha256.Should().NotBe(
                claimMaster[documentId].Sha256,
                because: "解消済みなら KnownDivergentDocumentIds から外すこと");
        }
    }

    private static string RecordIdOf(string fieldId)
    {
        var lastSeparator = fieldId.LastIndexOf(':');
        return lastSeparator < 0 ? fieldId : fieldId[..lastSeparator];
    }

    /// <summary>sources.json の SHA-256 を 1 文字ずらして「文書が差し替わった」状態を作る。</summary>
    private static CsvSpecificationCatalog LoadWithRotatedSha(string documentId)
    {
        using var sourcesStream = OpenResource("sources.json");
        using var reader = new StreamReader(sourcesStream);
        var sources = JsonSerializer.Deserialize<JsonElement>(
            reader.ReadToEnd(), SerializerOptions); // CultureInfo: 非該当（JSON）

        var registered = sources.GetProperty("sources").EnumerateArray()
            .Single(source => source.GetProperty("sourceDocumentId").GetString() == documentId)
            .GetProperty("sha256").GetString()!;
        var rotated = registered[0] == '0'
            ? string.Concat("1", registered.AsSpan(1))
            : string.Concat("0", registered.AsSpan(1));
        var mutated = sources.GetRawText().Replace(registered, rotated, StringComparison.Ordinal);

        using var common = OpenResource("common-r7-10.json");
        using var provider = OpenResource("provider-claim-r7-10.json");
        using var mapping = OpenResource("field-mapping-r7-10.json");
        using var evidence = OpenResource("spec-evidence-r7-10.json");
        using var mutatedSources = new MemoryStream(Encoding.UTF8.GetBytes(mutated));
        return CsvSpecificationLoader.Load(common, provider, mapping, mutatedSources, evidence);
    }

    private static Dictionary<string, (string Sha256, string Url)> ReadClaimMasterSources()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Tsumugi.Infrastructure",
            "ClaimMasters",
            "Seed",
            "sources.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("sources").EnumerateArray().ToDictionary(
            source => source.GetProperty("documentId").GetString()!,
            source => (
                source.GetProperty("sha256").GetString()!,
                source.GetProperty("url").GetString()!),
            StringComparer.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Tsumugi.sln")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("Tsumugi.sln must be reachable from the test output directory");
        return directory!.FullName;
    }

    private static ItemTableExtractionFile LoadExtraction()
    {
        using var stream = OpenResource("provider-r7-10-item-tables.json");
        using var reader = new StreamReader(stream);
        return JsonSerializer.Deserialize<ItemTableExtractionFile>(
            reader.ReadToEnd(), SerializerOptions)!; // CultureInfo: 非該当（JSON）
    }

    private static Stream OpenResource(string fileName)
    {
        var resourceName = CsvAssembly.GetManifestResourceNames()
            .Single(name => name.EndsWith($".Specifications.{fileName}", StringComparison.Ordinal));
        return CsvAssembly.GetManifestResourceStream(resourceName)!;
    }

    [GeneratedRegex(@"^p\.(\d+);item=(\d+)$")]
    private static partial Regex ItemLocator();

    [GeneratedRegex(@"source=([^;)\s]+)")]
    private static partial Regex RuleAnchor();

    [GeneratedRegex(@"^([^:]+):p(\d+):item(\d+)")]
    private static partial Regex ItemAnchor();

    internal sealed record ItemTableExtractionFile(IReadOnlyList<ExtractedRecordRows> Records);

    internal sealed record ExtractedRecordRows(string RecordId, IReadOnlyList<ExtractedRow> Items);

    internal sealed record ExtractedRow(int Position, int SourcePage);
}
