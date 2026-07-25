using System.Text;
using FluentAssertions;
using Tsumugi.Infrastructure.Csv.Specifications;
using Tsumugi.Infrastructure.Csv.Writer;

namespace Tsumugi.Infrastructure.Csv.Tests.Writer;

public sealed class CsvCellEncoderTests
{
    private static string Decode(ReadOnlyMemory<byte> bytes) =>
        CsvCellEncoder.Cp932.GetString(bytes.Span);

    /// <summary>
    /// 既定の属性区分は「英数」（半角）。全角の値を扱うテストは公式属性が「漢字」の項目
    /// （摘要・備考）を模して <paramref name="officialAttribute"/> を明示する。
    /// </summary>
    private static CsvFieldSpecification Field(
        string fieldId,
        int position = 1,
        string requiredWhen = "never",
        string dataType = "text",
        int maxBytes = 40,
        string quoteRule = CsvCellEncoder.ConditionalQuoteRule,
        IReadOnlyList<string>? allowedCodes = null,
        string officialAttribute = "英数") =>
        new(
            fieldId,
            position,
            OfficialName: "テスト項目",
            RequiredWhen: requiredWhen,
            OfficialAttribute: officialAttribute,
            DataType: dataType,
            MaxBytes: maxBytes,
            QuoteRule: quoteRule,
            AllowedCodes: allowedCodes ?? [],
            SourcePage: 1,
            RequiredWhenSource: "test");

    [Fact]
    public void EncodeCell_writes_ascii_content_without_quotes()
    {
        var spec = Field("provider:J111:01:005", dataType: "text", maxBytes: 10);

        var bytes = CsvCellEncoder.EncodeCell(new CsvCell(spec.FieldId, "1112223333"), spec);

        Decode(bytes).Should().Be("1112223333");
    }

    // 値は属性区分の文字種規則（共通編 1.3.2(1)③）を満たすものだけを使う。半角の英数字を含む値は
    // 英数属性の項目、全角を含む値は漢字属性の項目（摘要・備考）に置く。
    [Theory]
    [InlineData("つむぎ事業所", "\"つむぎ事業所\"", "漢字")] // 漢字を含む
    [InlineData("あ,い", "\"あ,い\"", "漢字")] // カンマ
    [InlineData("A B", "\"A B\"", "英数")] // 半角スペース
    [InlineData("あ　い", "\"あ　い\"", "漢字")] // 全角スペース
    [InlineData("山田\"太郎\"", "\"山田\"\"太郎\"\"\"", "漢字")] // 埋め込み二重引用符は二重化
    public void EncodeCell_quotes_only_when_the_official_quote_rule_requires_it(
        string raw, string expected, string officialAttribute)
    {
        var spec = Field("provider:J121:01:008", maxBytes: 40, officialAttribute: officialAttribute);

        Decode(CsvCellEncoder.EncodeCell(new CsvCell(spec.FieldId, raw), spec)).Should().Be(expected);
    }

    // 共通編 1.2.2(4) は「漢字」を 2 バイトコードとして定義する。全角カナ・全角記号も 2 バイトなので
    // 引用対象。半角カナ・半角数字は 1 バイトなので引用しない。
    // NOTE(teeth): 表意文字だけを見る実装へ戻すと、全角カナ氏名が引用されず仕様違反になる。
    [Theory]
    [InlineData("ヤマダタロウ")]   // 全角カナ = 2バイト
    [InlineData("株式会社")]       // 漢字
    [InlineData("１２３")]         // 全角数字
    [InlineData("Ａ")]             // 全角英字
    public void EncodeCell_quotes_every_two_byte_value(string raw)
    {
        // 2 バイト値を載せられるのは漢字属性の項目（摘要・備考）だけ。
        var spec = Field("provider:J611:02:035", maxBytes: 40, officialAttribute: "漢字");

        Decode(CsvCellEncoder.EncodeCell(new CsvCell(spec.FieldId, raw), spec))
            .Should().Be($"\"{raw}\"");
    }

    [Theory]
    [InlineData("ﾔﾏﾀﾞﾀﾛｳ")]
    [InlineData("12345")]
    [InlineData("ABC")]
    public void EncodeCell_does_not_quote_single_byte_values(string raw)
    {
        var spec = Field("provider:J121:01:008", maxBytes: 40);

        Decode(CsvCellEncoder.EncodeCell(new CsvCell(spec.FieldId, raw), spec)).Should().Be(raw);
    }

    // 共通編 1.2.2(3)① の使用不可能文字（シングルコーテーション 0x27）。
    [Fact]
    public void EncodeCell_fails_on_a_single_quotation_mark()
    {
        var spec = Field("provider:J121:01:008");

        var act = () => CsvCellEncoder.EncodeCell(new CsvCell(spec.FieldId, "O'Brien"), spec);

        act.Should().Throw<CsvEncodingException>()
            .Which.Reason.Should().Be(CsvEncodingReason.ProhibitedCharacter);
    }

    // maxBytes は「引用符を除いた内容のバイト数」の上限（provider:J611:01 の
    // sum(maxBytes)+カンマ = 822 = common:outer:data:003 のデータ長、という spec 内の一致から導出）。
    [Fact]
    public void EncodeCell_measures_byte_width_on_the_content_excluding_the_surrounding_quotes()
    {
        var spec = Field("provider:J111:01:006", maxBytes: 8, officialAttribute: "漢字");

        var bytes = CsvCellEncoder.EncodeCell(new CsvCell(spec.FieldId, "事業所名"), spec);

        bytes.Length.Should().Be(10); // 内容8バイト + 引用符2バイト
        Decode(bytes).Should().Be("\"事業所名\"");
    }

    [Fact]
    public void EncodeCell_fails_when_content_exceeds_the_byte_width()
    {
        var spec = Field("provider:J111:01:006", maxBytes: 7, officialAttribute: "漢字");

        var act = () => CsvCellEncoder.EncodeCell(new CsvCell(spec.FieldId, "事業所名"), spec);

        act.Should().Throw<CsvEncodingException>()
            .Which.Reason.Should().Be(CsvEncodingReason.OverByteWidth);
    }

    [Fact]
    public void EncodeCell_fails_on_a_character_that_cp932_cannot_represent()
    {
        var spec = Field("provider:J121:01:008");

        var act = () => CsvCellEncoder.EncodeCell(new CsvCell(spec.FieldId, "🍣"), spec);

        act.Should().Throw<CsvEncodingException>()
            .Which.Reason.Should().Be(CsvEncodingReason.NonRepresentableCharacter);
    }

    [Fact]
    public void EncodeCell_fails_on_a_nul_character()
    {
        var spec = Field("provider:J121:01:008");

        var act = () => CsvCellEncoder.EncodeCell(new CsvCell(spec.FieldId, "山田\0太郎"), spec);

        act.Should().Throw<CsvEncodingException>()
            .Which.Reason.Should().Be(CsvEncodingReason.NulCharacter);
    }

    [Theory]
    [InlineData("山田\r太郎")]
    [InlineData("山田\n太郎")]
    public void EncodeCell_fails_on_an_embedded_line_break(string raw)
    {
        var spec = Field("provider:J121:01:008");

        var act = () => CsvCellEncoder.EncodeCell(new CsvCell(spec.FieldId, raw), spec);

        act.Should().Throw<CsvEncodingException>()
            .Which.Reason.Should().Be(CsvEncodingReason.LineBreakInValue);
    }

    // NOTE(teeth): TAB や DEL は行構造を壊さないため素通りしやすいが、取込側で拒否されうる。
    [Theory]
    [InlineData("\t")]
    [InlineData("\u0007")]
    [InlineData("\u007f")]
    [InlineData("A\u001bB")]
    public void EncodeCell_fails_on_a_control_character(string raw)
    {
        var spec = Field("provider:J121:01:008");

        var act = () => CsvCellEncoder.EncodeCell(new CsvCell(spec.FieldId, raw), spec);

        act.Should().Throw<CsvEncodingException>()
            .Which.Reason.Should().Be(CsvEncodingReason.ControlCharacter);
    }

    // NOTE(teeth): 数値・年月・日付欄に記号や英字が混じったまま出すと、取込側で弾かれるか
    // 別の値として解釈される。
    [Theory]
    [InlineData("numeric", "12a")]
    [InlineData("numeric", "1,2")]
    [InlineData("numeric", "-1")]
    [InlineData("numeric", "1.5")]
    [InlineData("yearMonth", "2026-08")]
    [InlineData("date", "2026/07/01")]
    [InlineData("numeric", "１２３")]
    public void EncodeCell_fails_when_a_digit_only_data_type_receives_other_characters(
        string dataType, string raw)
    {
        var spec = Field("provider:J121:04:010", dataType: dataType, maxBytes: 12);

        var act = () => CsvCellEncoder.EncodeCell(new CsvCell(spec.FieldId, raw), spec);

        act.Should().Throw<CsvEncodingException>()
            .Which.Reason.Should().Be(CsvEncodingReason.InvalidCharacterForDataType);
    }

    [Theory]
    [InlineData("numeric", "0")]
    [InlineData("numeric", "3000")]
    [InlineData("yearMonth", "202608")]
    [InlineData("date", "20260701")]
    public void EncodeCell_accepts_digits_for_a_digit_only_data_type(string dataType, string raw)
    {
        var spec = Field("provider:J121:04:010", dataType: dataType, maxBytes: 12);

        Decode(CsvCellEncoder.EncodeCell(new CsvCell(spec.FieldId, raw), spec)).Should().Be(raw);
    }

    // 共通編 1.3.2(1)③ の属性区分ごとの文字種規則。
    // NOTE(teeth): 英数属性の項目（氏名カナ・事業所番号・交換情報識別番号など）へ全角を載せると
    // 「「英数」項目には漢字（2 バイトコード）を混在させない」に反し、取込側で弾かれる。
    [Theory]
    [InlineData("ヤマダタロウ")]  // 全角カナ = 2バイト
    [InlineData("山田")]          // 漢字
    [InlineData("１２３")]        // 全角数字
    [InlineData("Ａ")]            // 全角英字
    [InlineData("　")]            // 全角スペース
    [InlineData("abc")]           // 半角英小文字は使用できない
    [InlineData("A-1")]           // 英字・数字・カナ以外の記号
    public void EncodeCell_fails_when_an_alphanumeric_field_receives_a_disallowed_character(string raw)
    {
        var spec = Field("provider:J121:01:008", maxBytes: 40, officialAttribute: "英数");

        var act = () => CsvCellEncoder.EncodeCell(new CsvCell(spec.FieldId, raw), spec);

        act.Should().Throw<CsvEncodingException>()
            .Which.Reason.Should().Be(CsvEncodingReason.InvalidCharacterForOfficialAttribute);
    }

    [Theory]
    [InlineData("ﾂﾑｷﾞﾀﾛｳ", "ﾂﾑｷﾞﾀﾛｳ")]          // 半角カナ
    [InlineData("ﾂﾑｷﾞ ﾀﾛｳ", "\"ﾂﾑｷﾞ ﾀﾛｳ\"")]   // 半角カナ＋スペース（設定例が示す形。スペースは引用対象）
    [InlineData("J111", "J111")]                  // 半角英大文字＋数字
    [InlineData("1312345678", "1312345678")]      // 数字
    public void EncodeCell_accepts_single_byte_content_for_an_alphanumeric_field(
        string raw, string expected)
    {
        var spec = Field("provider:J121:01:008", maxBytes: 40, officialAttribute: "英数");

        Decode(CsvCellEncoder.EncodeCell(new CsvCell(spec.FieldId, raw), spec)).Should().Be(expected);
    }

    // NOTE(teeth): 漢字属性の項目（摘要・備考）に半角英数字・半角カナを混ぜると仕様違反。
    [Theory]
    [InlineData("3日")]
    [InlineData("A欄")]
    [InlineData("ﾒﾓ")]
    public void EncodeCell_fails_when_a_kanji_field_receives_single_byte_letters_digits_or_kana(string raw)
    {
        var spec = Field("provider:J611:02:035", maxBytes: 100, officialAttribute: "漢字");

        var act = () => CsvCellEncoder.EncodeCell(new CsvCell(spec.FieldId, raw), spec);

        act.Should().Throw<CsvEncodingException>()
            .Which.Reason.Should().Be(CsvEncodingReason.InvalidCharacterForOfficialAttribute);
    }

    // コード値属性は「0，1，2，～，9 の数字1桁をそれぞれ1バイトで表す」。dataType=code は従来
    // 文字種を検証していなかったため、属性区分の側で強制する。
    [Theory]
    [InlineData("J121")]
    [InlineData("4６")]
    [InlineData("1A")]
    public void EncodeCell_fails_when_a_code_value_field_receives_non_digits(string raw)
    {
        var spec = Field(
            "provider:J121:01:014", dataType: "code", maxBytes: 4, officialAttribute: "コード値");

        var act = () => CsvCellEncoder.EncodeCell(new CsvCell(spec.FieldId, raw), spec);

        act.Should().Throw<CsvEncodingException>()
            .Which.Reason.Should().Be(CsvEncodingReason.InvalidCharacterForOfficialAttribute);
    }

    [Fact]
    public void EncodeCell_fails_on_an_unknown_official_attribute()
    {
        var spec = Field("provider:J121:01:008", officialAttribute: "全角英数");

        var act = () => CsvCellEncoder.EncodeCell(new CsvCell(spec.FieldId, "1"), spec);

        act.Should().Throw<CsvEncodingException>()
            .Which.Reason.Should().Be(CsvEncodingReason.UnknownOfficialAttribute);
    }

    // NOTE(teeth): spec JSON に未知の属性区分が現れたら encoder が fail-close することを、
    // 実データ側から固定する（公式 4 区分＋可変長ペイロードの空欄だけが現れる）。
    [Fact]
    public void Every_official_attribute_in_the_embedded_specification_is_known_to_the_encoder()
    {
        var catalog = CsvSpecificationLoader.LoadEmbedded();

        var attributes = catalog.CommonRecords.Concat(catalog.ProviderRecords)
            .SelectMany(record => record.Fields)
            .Select(field => field.OfficialAttribute)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        attributes.Should().BeSubsetOf(["英数", "数値", "コード値", "漢字", string.Empty]);
        attributes.Should().Contain("漢字");
    }

    [Fact]
    public void EncodeCell_fails_when_the_cell_field_id_does_not_match_the_specification()
    {
        var spec = Field("provider:J121:01:008");

        var act = () => CsvCellEncoder.EncodeCell(new CsvCell("provider:J121:01:009", "x"), spec);

        act.Should().Throw<CsvEncodingException>()
            .Which.Reason.Should().Be(CsvEncodingReason.FieldIdMismatch);
    }

    [Fact]
    public void EncodeCell_fails_when_the_value_is_not_an_allowed_code()
    {
        var spec = Field("provider:J111:01:001", dataType: "code", maxBytes: 4, allowedCodes: ["J111"]);

        var act = () => CsvCellEncoder.EncodeCell(new CsvCell(spec.FieldId, "J121"), spec);

        act.Should().Throw<CsvEncodingException>()
            .Which.Reason.Should().Be(CsvEncodingReason.UnknownCode);
    }

    [Fact]
    public void EncodeCell_accepts_an_allowed_code()
    {
        var spec = Field("provider:J111:01:001", dataType: "code", maxBytes: 4, allowedCodes: ["J111"]);

        Decode(CsvCellEncoder.EncodeCell(new CsvCell(spec.FieldId, "J111"), spec)).Should().Be("J111");
    }

    [Fact]
    public void EncodeCell_fails_when_an_always_required_field_is_empty()
    {
        var spec = Field("provider:J111:01:005", requiredWhen: "always", maxBytes: 10);

        var act = () => CsvCellEncoder.EncodeCell(new CsvCell(spec.FieldId, string.Empty), spec);

        act.Should().Throw<CsvEncodingException>()
            .Which.Reason.Should().Be(CsvEncodingReason.MissingRequired);
    }

    [Fact]
    public void EncodeCell_accepts_an_empty_value_for_a_never_required_field()
    {
        var spec = Field("provider:J111:01:020", requiredWhen: "never", maxBytes: 6);

        CsvCellEncoder.EncodeCell(new CsvCell(spec.FieldId, string.Empty), spec).Length.Should().Be(0);
    }

    [Fact]
    public void EncodeCell_writes_crlf_for_the_trailing_blank_field()
    {
        var spec = Field(
            "common:outer:end:003",
            position: 3,
            requiredWhen: "always",
            maxBytes: 2,
            quoteRule: CsvCellEncoder.CrlfQuoteRule);

        CsvCellEncoder.EncodeCell(new CsvCell(spec.FieldId, string.Empty), spec)
            .ToArray().Should().Equal((byte)'\r', (byte)'\n');
    }

    [Fact]
    public void EncodeCell_fails_when_a_crlf_field_carries_a_value()
    {
        var spec = Field(
            "common:outer:end:003",
            requiredWhen: "always",
            maxBytes: 2,
            quoteRule: CsvCellEncoder.CrlfQuoteRule);

        var act = () => CsvCellEncoder.EncodeCell(new CsvCell(spec.FieldId, "x"), spec);

        act.Should().Throw<CsvEncodingException>()
            .Which.Reason.Should().Be(CsvEncodingReason.CrlfFieldMustBeEmpty);
    }

    [Fact]
    public void EncodeCell_fails_on_an_unknown_quote_rule()
    {
        var spec = Field("provider:J121:01:008", quoteRule: "surround with backticks");

        var act = () => CsvCellEncoder.EncodeCell(new CsvCell(spec.FieldId, "x"), spec);

        act.Should().Throw<CsvEncodingException>()
            .Which.Reason.Should().Be(CsvEncodingReason.UnknownQuoteRule);
    }

    [Fact]
    public void EncodeFields_joins_cells_with_commas()
    {
        var specs = new[]
        {
            Field("a", position: 1, dataType: "numeric", maxBytes: 4, officialAttribute: "数値"),
            Field("b", position: 2, maxBytes: 20, officialAttribute: "漢字"),
            Field("c", position: 3, dataType: "numeric", maxBytes: 6),
        };
        var cells = new[]
        {
            new CsvCell("a", "1"),
            new CsvCell("b", "試験"),
            new CsvCell("c", "10"),
        };

        Decode(CsvCellEncoder.EncodeFields(cells, specs)).Should().Be("1,\"試験\",10");
    }

    // 末尾の crlf 項目はカンマで区切らず、そのまま行終端になる。
    [Fact]
    public void EncodeFields_terminates_with_crlf_without_a_preceding_comma()
    {
        var specs = new[]
        {
            Field("common:outer:end:001", position: 1, requiredWhen: "always", dataType: "code", maxBytes: 1, allowedCodes: ["3"]),
            Field("common:outer:end:002", position: 2, requiredWhen: "always", dataType: "numeric", maxBytes: 9),
            Field("common:outer:end:003", position: 3, requiredWhen: "always", maxBytes: 2, quoteRule: CsvCellEncoder.CrlfQuoteRule),
        };
        var cells = new[]
        {
            new CsvCell("common:outer:end:001", "3"),
            new CsvCell("common:outer:end:002", "12"),
            new CsvCell("common:outer:end:003", string.Empty),
        };

        Decode(CsvCellEncoder.EncodeFields(cells, specs)).Should().Be("3,12\r\n");
    }

    [Fact]
    public void EncodeFields_fails_when_the_cell_count_differs_from_the_specification()
    {
        var specs = new[] { Field("a"), Field("b", position: 2) };
        var cells = new[] { new CsvCell("a", "1") };

        var act = () => CsvCellEncoder.EncodeFields(cells, specs);

        act.Should().Throw<CsvEncodingException>()
            .Which.Reason.Should().Be(CsvEncodingReason.FieldCountMismatch);
    }

    // NOTE(teeth): spec JSON に未知の quoteRule が現れたら encoder が fail-close することを、
    // 実データ側から固定する。新しい quoteRule が追加されたらここが RED になる。
    [Fact]
    public void Every_quote_rule_in_the_embedded_specification_is_known_to_the_encoder()
    {
        var catalog = CsvSpecificationLoader.LoadEmbedded();

        var rules = catalog.CommonRecords.Concat(catalog.ProviderRecords)
            .SelectMany(record => record.Fields)
            .Select(field => field.QuoteRule)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        rules.Should().OnlyContain(rule =>
            rule == CsvCellEncoder.ConditionalQuoteRule || rule == CsvCellEncoder.CrlfQuoteRule);
    }

    // NOTE(teeth): crlf 項目は「各外側レコードの最終項目」だけに現れるという構造前提を固定する。
    [Fact]
    public void The_crlf_quote_rule_appears_only_on_the_last_field_of_a_record()
    {
        var catalog = CsvSpecificationLoader.LoadEmbedded();

        foreach (var record in catalog.CommonRecords.Concat(catalog.ProviderRecords))
        {
            var crlfPositions = record.Fields
                .Where(field => field.QuoteRule == CsvCellEncoder.CrlfQuoteRule)
                .Select(field => field.Position)
                .ToArray();

            crlfPositions.Should().BeSubsetOf([record.Fields.Count]);
        }
    }

    [Fact]
    public void Cp932_is_registered_and_round_trips_japanese_text()
    {
        CsvCellEncoder.Cp932.CodePage.Should().Be(932);
        CsvCellEncoder.Cp932.GetString(CsvCellEncoder.Cp932.GetBytes("紡ぎ")).Should().Be("紡ぎ");
        Encoding.GetEncoding(932).GetByteCount("あ").Should().Be(2);
    }
}
