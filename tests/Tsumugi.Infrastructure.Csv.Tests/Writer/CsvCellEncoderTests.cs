using System.Text;
using FluentAssertions;
using Tsumugi.Infrastructure.Csv.Specifications;
using Tsumugi.Infrastructure.Csv.Writer;

namespace Tsumugi.Infrastructure.Csv.Tests.Writer;

public sealed class CsvCellEncoderTests
{
    private static string Decode(ReadOnlyMemory<byte> bytes) =>
        CsvCellEncoder.Cp932.GetString(bytes.Span);

    private static CsvFieldSpecification Field(
        string fieldId,
        int position = 1,
        string requiredWhen = "never",
        string dataType = "text",
        int maxBytes = 40,
        string quoteRule = CsvCellEncoder.ConditionalQuoteRule,
        IReadOnlyList<string>? allowedCodes = null) =>
        new(
            fieldId,
            position,
            OfficialName: "テスト項目",
            RequiredWhen: requiredWhen,
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

    [Theory]
    [InlineData("つむぎ事業所", "\"つむぎ事業所\"")] // 漢字を含む
    [InlineData("あ,い", "\"あ,い\"")] // カンマ
    [InlineData("A B", "\"A B\"")] // 半角スペース
    [InlineData("A　B", "\"A　B\"")] // 全角スペース
    [InlineData("山田\"太郎\"", "\"山田\"\"太郎\"\"\"")] // 埋め込み二重引用符は二重化
    public void EncodeCell_quotes_only_when_the_official_quote_rule_requires_it(string raw, string expected)
    {
        var spec = Field("provider:J121:01:008", maxBytes: 40);

        Decode(CsvCellEncoder.EncodeCell(new CsvCell(spec.FieldId, raw), spec)).Should().Be(expected);
    }

    // 公式ルールの文言は「comma, double quote, space, or kanji」。カナだけの値は引用しない。
    // ここが緩む/厳しくなると RED になり、規則の解釈変更に気付ける。
    [Theory]
    [InlineData("ヤマダタロウ")]
    [InlineData("ﾔﾏﾀﾞﾀﾛｳ")]
    [InlineData("12345")]
    public void EncodeCell_does_not_quote_kana_or_digits(string raw)
    {
        var spec = Field("provider:J121:01:008", maxBytes: 40);

        Decode(CsvCellEncoder.EncodeCell(new CsvCell(spec.FieldId, raw), spec)).Should().Be(raw);
    }

    // maxBytes は「引用符を除いた内容のバイト数」の上限（provider:J611:01 の
    // sum(maxBytes)+カンマ = 822 = common:outer:data:003 のデータ長、という spec 内の一致から導出）。
    [Fact]
    public void EncodeCell_measures_byte_width_on_the_content_excluding_the_surrounding_quotes()
    {
        var spec = Field("provider:J111:01:006", maxBytes: 8);

        var bytes = CsvCellEncoder.EncodeCell(new CsvCell(spec.FieldId, "事業所名"), spec);

        bytes.Length.Should().Be(10); // 内容8バイト + 引用符2バイト
        Decode(bytes).Should().Be("\"事業所名\"");
    }

    [Fact]
    public void EncodeCell_fails_when_content_exceeds_the_byte_width()
    {
        var spec = Field("provider:J111:01:006", maxBytes: 7);

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
            Field("a", position: 1, dataType: "numeric", maxBytes: 4),
            Field("b", position: 2, maxBytes: 20),
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
