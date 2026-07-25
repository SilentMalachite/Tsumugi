using FluentAssertions;
using Tsumugi.Application.Claim;
using Tsumugi.Infrastructure.Csv.Generation;
using Tsumugi.Infrastructure.Csv.Specifications;
using Tsumugi.Infrastructure.Csv.Writer;

namespace Tsumugi.Infrastructure.Csv.Tests.Generation;

/// <summary>
/// 公式の属性区分（共通編 1.3.2(1)③）と、コントロールレコードの設定値（同 物理6頁の項目表）に
/// 対する適合を、実データの生成結果から固定する。
/// </summary>
/// <remarks>
/// 属性区分の文字種規則は <c>CsvCellEncoder</c> の単体テストで網羅する。ここでは
/// <b>実際の請求 CSV が規則を満たすこと</b>と、満たせない入力で fail-close することを固定する。
/// </remarks>
public sealed class OfficialAttributeConformanceTests
{
    private static readonly CsvSpecificationCatalog Catalog = CsvSpecificationLoader.LoadEmbedded();

    private static string[] Lines(byte[] bytes) =>
        CsvCellEncoder.Cp932.GetString(bytes).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

    /// <summary>データレコードの「データ」項目（内側レコード）を取り出す。</summary>
    private static string[] InnerRecords(byte[] bytes) =>
        [.. Lines(bytes)[1..^1].Select(line => string.Join(',', line.Split(',')[2..]))];

    private static string InnerField(byte[] bytes, string exchangeId, string recordType, int position) =>
        InnerRecords(bytes)
            .Single(record => record.StartsWith($"{exchangeId},{recordType},", StringComparison.Ordinal))
            .Split(',')[position - 1];

    // 支給決定者氏名カナ（provider:J121:01:008）の公式属性は英数＝半角 1 バイト。
    // NOTE(teeth): 全角カナをそのまま出す実装へ戻すと、ここが「引用符つきの全角」になって落ちる。
    [Fact]
    public void A_full_width_kana_name_is_written_in_half_width_kana()
    {
        var dto = ClaimCsvFixtures.Normal() with
        {
            Recipients = [ClaimCsvFixtures.Recipient("1234567890", kanaName: "ツムギタロウ")],
        };

        var bytes = new ClaimCsvGenerator(Catalog).Generate(dto).Bytes;

        // 半角なので 2 バイトコードを含まず、引用符も付かない（共通編 1.2.2(4)）。
        // 濁音は「基底＋濁点」の 2 文字になる（ギ → ｷﾞ）。
        InnerField(bytes, "J121", "01", 8).Should().Be("ﾂﾑｷﾞﾀﾛｳ");
    }

    [Fact]
    public void A_half_width_kana_name_is_written_unchanged()
    {
        var bytes = new ClaimCsvGenerator(Catalog).Generate(ClaimCsvFixtures.Normal()).Bytes;

        // fixture の既定は "ﾂﾑｷﾞ ﾀﾛｳ"。半角スペースを含むため引用符が付く。
        InnerField(bytes, "J121", "01", 8).Should().Be("\"ﾂﾑｷﾞ ﾀﾛｳ\"");
    }

    // 半角形を持たない文字（ひらがな・漢字・康熙部首）は丸めずに fail-close する。
    [Theory]
    [InlineData("つむぎたろう")]
    [InlineData("紡太郎")]
    [InlineData("⼀ﾉｾ ﾀﾛｳ")]
    public void A_name_without_a_half_width_form_fails_closed_with_the_field_id(string kanaName)
    {
        var dto = ClaimCsvFixtures.Normal() with
        {
            Recipients = [ClaimCsvFixtures.Recipient("1234567890", kanaName: kanaName)],
        };

        var act = () => new ClaimCsvGenerator(Catalog).Generate(dto);

        var exception = act.Should().Throw<ClaimCsvExportFailedException>().Which;
        exception.FieldId.Should().Be("provider:J121:01:008");
        exception.Reason.Should().Be("UnresolvableModelPath");
        // 氏名そのものは例外に載せない（CLAUDE.md §ハード制約4）。
        exception.Message.Should().NotContain(kanaName);
        exception.Detail.Should().NotContain(kanaName);
    }

    /// <summary>
    /// 生成した CSV の全セルが、その項目の属性区分が許す文字だけで構成されている。
    /// </summary>
    [Theory]
    [InlineData("normal")]
    [InlineData("multi")]
    public void Every_cell_of_a_generated_file_satisfies_its_official_attribute(string scenario)
    {
        var dto = scenario switch
        {
            "multi" => ClaimCsvFixtures.Normal() with
            {
                Recipients =
                [
                    ClaimCsvFixtures.Recipient("1000000001"),
                    ClaimCsvFixtures.Recipient("1000000002", kanaName: "ﾂﾑｷﾞ ﾊﾅｺ"),
                ],
            },
            _ => ClaimCsvFixtures.Normal(),
        };

        var bytes = new ClaimCsvGenerator(Catalog).Generate(dto).Bytes;

        // 内側レコードは交換情報識別番号＋レコード種別で spec の record を引ける。
        var recordsById = Catalog.ProviderRecords.ToDictionary(
            record => $"{record.ExchangeInformationId},{record.InnerRecordType}",
            StringComparer.Ordinal);
        var checkedCells = 0;
        foreach (var inner in InnerRecords(bytes))
        {
            var cells = inner.Split(',');
            var record = recordsById[$"{cells[0]},{cells[1]}"];
            foreach (var field in record.Fields)
            {
                var raw = cells[field.Position - 1].Trim('"').Replace("\"\"", "\"", StringComparison.Ordinal);
                if (raw.Length == 0) continue;

                CsvOfficialAttribute.IndexOfDisallowedCharacter(field.OfficialAttribute, raw)
                    .Should().Be(
                        -1,
                        $"{field.FieldId}（{field.OfficialName}）は属性 '{field.OfficialAttribute}'");
                checkedCells++;
            }
        }

        checkedCells.Should().BeGreaterThan(0);
    }

    // 共通編 物理6頁 コントロールレコード 項番 6〜8。「送付元または送付先が◯◯の場合は◯◯番号を
    // 設定、◯◯以外の場合は 0 を設定する」。事業所からの請求では送付元が事業所なので、
    // 事業所番号だけが実値で、市町村番号・都道府県番号は 0 になる。
    // NOTE(teeth): コード値属性の「全桁 0 は未設定」を一律にエラー扱いする guard を入れると、
    // 公式の指示どおりの 0 が出せなくなる（項目の内容欄が優先する）。
    [Fact]
    public void The_control_record_identifies_the_provider_and_zeroes_the_other_senders()
    {
        var bytes = new ClaimCsvGenerator(Catalog).Generate(ClaimCsvFixtures.Normal()).Bytes;

        var control = Lines(bytes)[0].Split(',');
        control[PositionOf("common:outer:control:006") - 1].Should().Be("0");
        control[PositionOf("common:outer:control:007") - 1].Should().Be("1312345678");
        control[PositionOf("common:outer:control:008") - 1].Should().Be("0");
    }

    private static int PositionOf(string fieldId) =>
        Catalog.CommonRecords.SelectMany(record => record.Fields)
            .Single(field => string.Equals(field.FieldId, fieldId, StringComparison.Ordinal))
            .Position;
}
