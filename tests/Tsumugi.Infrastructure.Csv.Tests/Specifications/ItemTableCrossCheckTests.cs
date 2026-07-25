using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Tsumugi.Infrastructure.Csv.Specifications;

namespace Tsumugi.Infrastructure.Csv.Tests.Specifications;

/// <summary>
/// 運用に使う CSV 仕様（<c>provider-claim-r7-10.json</c>）と、インタフェース仕様書 事業所編の
/// 項目表を機械抽出した結果（<c>provider-r7-10-item-tables.json</c>）を突合する。
/// </summary>
/// <remarks>
/// <para>
/// 運用側の spec は人が PDF を読んで書き起こしたもので、書き起こし誤りは「誰かがもう一度 PDF を
/// 読む」ときにしか見つからなかった（Phase 3-3 で単位・日数定義・レコードの性質・コード表の誤りが
/// 後から発覚した）。抽出結果を checked-in して突合することで、次の施行分では
/// <b>340 頁を読み直すのではなく抽出結果の差分をレビューする</b>運用に切り替える。
/// </para>
/// <para>
/// 抽出は <c>build/extract_interface_item_tables.py</c>（罫線からセルを決める決定論的な抽出）。
/// PDF はリポジトリに置かないため、再生成には <c>sources.json</c> の URL から取得した PDF が必要。
/// このテスト自体は checked-in の抽出結果だけを読むので PDF 無しで動く。
/// </para>
/// <para>
/// NOTE(teeth): 抽出結果を運用 spec に合わせて手で書き換えると突合の意味が消える。抽出結果は
/// スクリプトの出力だけを入れること（`sourceSha256` が登録文書と一致することもここで固定する）。
/// </para>
/// </remarks>
public sealed class ItemTableCrossCheckTests
{
    /// <summary>突合対象の文書と、その抽出結果ファイル。</summary>
    public static TheoryData<string, string> Documents => new()
    {
        { "provider-r7-10", "provider-r7-10-item-tables.json" },
        { "common-r7-10", "common-r7-10-item-tables.json" },
    };

    /// <summary>
    /// 抽出結果と運用 spec が一致しないことが<b>分かっていて理由がある</b>項目。
    /// 黙って無視せず、理由つきで宣言する（文書が変わればこの前提も再確認する）。
    /// </summary>
    private static readonly Dictionary<string, string> DeclaredNameDifferences = new(StringComparer.Ordinal)
    {
        ["common:outer:control:003"] =
            "公式表は「ボリュ－ム通番」と全角ハイフンマイナス(U+FF0D)で組まれている。"
            + "spec は長音符(U+30FC)の「ボリューム通番」へ正規化している。",
    };

    /// <summary>
    /// 桁数が公式表に<b>記載されていない</b>項目（spec 側は構造から導出した値を持つ）。
    /// 根拠は証跡台帳（<c>spec-evidence-r7-10.json</c>）に置く。
    /// </summary>
    private static readonly Dictionary<string, string> DeclaredMissingByteLengths =
        new(StringComparer.Ordinal)
        {
            ["common:outer:data:003"] =
                "データレコードのペイロードは可変長で、公式表は属性・ﾊﾞｲﾄ数を空欄にしている。"
                + "spec の 822 は内側レコードの項目長とカンマ数から導出した値（ADR 0038 の証跡台帳を参照）。",
        };

    /// <summary>
    /// 公式の属性区分（共通編 1.2.3③）と spec の <c>dataType</c> の対応。現時点で観測される組み合わせを
    /// 固定する。日付・年月は公式には「コード値」属性である。
    /// </summary>
    private static readonly Dictionary<string, string[]> DataTypesByOfficialAttribute = new(StringComparer.Ordinal)
    {
        ["数値"] = ["numeric"],
        ["コード値"] = ["code", "date", "yearMonth"],
        ["英数"] = ["text"],
        ["漢字"] = ["text"],
    };

    private static readonly Assembly CsvAssembly = typeof(CsvSpecificationCatalog).Assembly;

    // SerializerOptions は Extraction より前に宣言する（静的フィールドは宣言順に初期化されるため、
    // 後ろに置くと LoadExtraction 実行時に null が渡り、既定の大文字小文字区別で全項目が未束縛になる）。
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private static readonly CsvSpecificationCatalog Catalog = CsvSpecificationLoader.LoadEmbedded();

    [Theory]
    [MemberData(nameof(Documents))]
    public void The_extraction_is_tied_to_the_registered_source_document(string documentId, string fileName)
    {
        var extraction = LoadExtraction(fileName);

        extraction.SourceDocumentId.Should().Be(documentId);
        extraction.SourceSha256.Should().Be(RegisteredSha256(documentId));
    }

    [Theory]
    [MemberData(nameof(Documents))]
    public void Every_record_in_the_operational_spec_is_covered_by_the_extraction(
        string documentId, string fileName)
    {
        var operational = RecordsOf(documentId).Select(record => record.RecordId).ToArray();
        var extracted = LoadExtraction(fileName).Records.Select(record => record.RecordId).ToArray();

        extracted.Should().BeEquivalentTo(operational);
    }

    [Theory]
    [MemberData(nameof(Documents))]
    public void Every_field_position_matches_the_official_item_numbers(string documentId, string fileName)
    {
        foreach (var record in RecordsOf(documentId))
        {
            var extracted = RecordFor(LoadExtraction(fileName), record.RecordId);
            extracted.Items.Select(item => item.Position).Should().BeEquivalentTo(
                record.Fields.Select(field => field.Position),
                because: $"{record.RecordId} の項番集合は公式の項目表と一致しなければならない");
        }
    }

    // NOTE(teeth): 桁数の書き起こし誤りはここで落ちる（事業所編 424 項目＋共通編 19 項目）。
    [Theory]
    [MemberData(nameof(Documents))]
    public void Every_max_byte_length_matches_the_official_byte_length(string documentId, string fileName)
    {
        foreach (var (record, field, item) in JoinedFields(documentId, fileName))
        {
            var fieldId = $"{record.RecordId}:{field.Position:D3}";
            if (DeclaredMissingByteLengths.TryGetValue(fieldId, out var reason))
            {
                item.ByteLength.Should().BeNull(because: reason);
                continue;
            }

            field.MaxBytes.Should().Be(
                item.ByteLength, because: $"{fieldId}（{item.OfficialName}）の桁数");
        }
    }

    /// <remarks>
    /// 集計欄などの項目名は「群ラベル＋個別名」を人が連結したもので、PDF 側に 1 つの正準文字列が
    /// 無い（群ラベルは行結合された縦書きセル）。そのため抽出側は<b>個別名だけ</b>を持ち、
    /// 突合は「個別名が運用 spec の項目名に含まれるか」で行う。
    /// </remarks>
    [Theory]
    [MemberData(nameof(Documents))]
    public void Every_official_name_contains_the_extracted_item_name(string documentId, string fileName)
    {
        foreach (var (record, field, item) in JoinedFields(documentId, fileName))
        {
            var fieldId = $"{record.RecordId}:{field.Position:D3}";
            if (DeclaredNameDifferences.TryGetValue(fieldId, out var reason))
            {
                Compact(field.OfficialName).Should().NotBe(
                    Compact(item.OfficialName), because: reason);
                continue;
            }

            Compact(field.OfficialName).Should().Contain(
                Compact(item.OfficialName), because: $"{fieldId} の項目名");
        }
    }

    [Theory]
    [MemberData(nameof(Documents))]
    public void Every_official_attribute_is_one_of_the_four_official_classes(
        string documentId, string fileName)
    {
        _ = documentId;
        var observed = LoadExtraction(fileName).Records
            .SelectMany(record => record.Items)
            .Select(item => item.OfficialAttribute)
            // 属性が空欄の項目（可変長ペイロード）は公式表に属性の記載が無い。
            .Where(attribute => attribute.Length > 0)
            .Distinct(StringComparer.Ordinal);

        observed.Should().BeSubsetOf(DataTypesByOfficialAttribute.Keys);
    }

    // 属性区分ごとに使える dataType を固定する。単位や文字種の取り違えは
    // ここで一段目の網にかかる（例: コード値属性の項目を numeric として扱う等）。
    [Theory]
    [MemberData(nameof(Documents))]
    public void Every_data_type_agrees_with_the_official_attribute(string documentId, string fileName)
    {
        foreach (var (record, field, item) in JoinedFields(documentId, fileName))
        {
            if (item.OfficialAttribute.Length == 0) continue;

            DataTypesByOfficialAttribute[item.OfficialAttribute].Should().Contain(
                field.DataType,
                because: $"{record.RecordId}:{field.Position:D3} は公式属性 '{item.OfficialAttribute}'");
        }
    }

    [Theory]
    [MemberData(nameof(Documents))]
    public void Every_extracted_item_carries_the_official_description(string documentId, string fileName)
    {
        _ = documentId;
        // 項目の説明（単位・カウント規則・設定値の根拠）が空の行があると、次の施行分で
        // 「差分を見る」運用が成立しない。
        LoadExtraction(fileName).Records.SelectMany(record => record.Items)
            .Where(item => item.OfficialNote.Length == 0)
            .Should().BeEmpty();
    }

    private static IEnumerable<(CsvRecordSpecification Record, CsvFieldSpecification Field, ExtractedItem Item)>
        JoinedFields(string documentId, string fileName)
    {
        var extraction = LoadExtraction(fileName);
        foreach (var record in RecordsOf(documentId))
        {
            var extracted = RecordFor(extraction, record.RecordId).Items
                .ToDictionary(item => item.Position);
            foreach (var field in record.Fields)
            {
                extracted.Should().ContainKey(field.Position);
                yield return (record, field, extracted[field.Position]);
            }
        }
    }

    private static IEnumerable<CsvRecordSpecification> RecordsOf(string documentId) =>
        Catalog.CommonRecords.Concat(Catalog.ProviderRecords)
            .Where(record => string.Equals(record.SourceDocumentId, documentId, StringComparison.Ordinal));

    private static ExtractedRecord RecordFor(ItemTableExtraction extraction, string recordId) =>
        extraction.Records.Single(
            record => string.Equals(record.RecordId, recordId, StringComparison.Ordinal));

    /// <summary>空白を落として比較する（PDF の項目名は字間が空くことがある）。</summary>
    private static string Compact(string value) =>
        string.Concat(value.Where(character => !char.IsWhiteSpace(character)));

    private static string RegisteredSha256(string documentId)
    {
        using var document = JsonDocument.Parse(ReadResource("sources.json"));
        return document.RootElement.GetProperty("sources").EnumerateArray()
            .Single(source => source.GetProperty("sourceDocumentId").GetString() == documentId)
            .GetProperty("sha256").GetString()!;
    }

    private static ItemTableExtraction LoadExtraction(string fileName) =>
        JsonSerializer.Deserialize<ItemTableExtraction>(ReadResource(fileName), SerializerOptions)!;

    private static string ReadResource(string fileName)
    {
        var resourceName = CsvAssembly.GetManifestResourceNames()
            .Single(name => name.EndsWith(fileName, StringComparison.Ordinal));
        using var stream = CsvAssembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd(); // CultureInfo: 非該当（UTF-8 の JSON をそのまま読む）
    }

    internal sealed record ItemTableExtraction(
        int SchemaVersion,
        int ExtractorVersion,
        string SourceDocumentId,
        string SourceSha256,
        IReadOnlyList<ExtractedRecord> Records);

    internal sealed record ExtractedRecord(
        string RecordId,
        IReadOnlyList<int> SourcePages,
        IReadOnlyList<ExtractedItem> Items);

    internal sealed record ExtractedItem(
        int Position,
        string OfficialName,
        string OfficialAttribute,
        [property: JsonConverter(typeof(NullableIntConverter))] int? ByteLength,
        string OfficialNote,
        string Remark);

    /// <summary>桁数欄が空の行（公式表に桁数の記載が無い行）を許容するための converter。</summary>
    internal sealed class NullableIntConverter : JsonConverter<int?>
    {
        public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => reader.TokenType == JsonTokenType.Null ? null : reader.GetInt32();

        public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(writer);
            if (value is { } number)
            {
                writer.WriteNumberValue(number);
                return;
            }

            writer.WriteNullValue();
        }
    }

    static ItemTableCrossCheckTests()
    {
        // 比較は序数のみ。カルチャ依存の大小比較・書式化を使わない。
        _ = CultureInfo.InvariantCulture;
    }
}
