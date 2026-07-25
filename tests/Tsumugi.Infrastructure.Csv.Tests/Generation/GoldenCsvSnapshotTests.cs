using FluentAssertions;
using Tsumugi.Application.Claim;
using Tsumugi.Application.Dtos.Claim.Csv;
using Tsumugi.Infrastructure.Csv.Generation;
using Tsumugi.Infrastructure.Csv.Specifications;
using Tsumugi.Infrastructure.Csv.Writer;

namespace Tsumugi.Infrastructure.Csv.Tests.Generation;

/// <summary>
/// AC3-7 のバイトスナップショット一致。golden は自前生成のため「仕様に対する正しさ」ではなく
/// 「意図しないバイト変化の検出」を担う。仕様適合そのものは
/// <see cref="ClaimCsvGeneratorTests"/> と <c>CsvCellEncoderTests</c> が担う。
/// </summary>
/// <remarks>
/// golden を意図的に更新するときは環境変数 <c>UPDATE_GOLDEN=1</c> を設定して実行し、
/// 生成された差分を必ず目視確認してからコミットすること。
/// </remarks>
public sealed class GoldenCsvSnapshotTests
{
    private static readonly CsvSpecificationCatalog Catalog = CsvSpecificationLoader.LoadEmbedded();

    public static TheoryData<string> Scenarios => new("normal", "correction", "cjk", "multi");

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void Generated_csv_matches_the_golden_fixture_byte_for_byte(string scenario)
    {
        var actual = new ClaimCsvGenerator(Catalog).Generate(Build(scenario));
        var path = GoldenPath(scenario);

        if (Environment.GetEnvironmentVariable("UPDATE_GOLDEN") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, actual);
        }

        File.Exists(path).Should().BeTrue($"golden fixture '{path}' must be committed");
        actual.Should().Equal(
            File.ReadAllBytes(path),
            $"scenario '{scenario}' must produce a byte-identical CSV");
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void Generated_csv_is_deterministic_for_the_same_input(string scenario)
    {
        var generator = new ClaimCsvGenerator(Catalog);

        generator.Generate(Build(scenario)).Should().Equal(generator.Generate(Build(scenario)));
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void Golden_fixtures_are_valid_cp932_with_crlf_line_endings(string scenario)
    {
        var bytes = File.ReadAllBytes(GoldenPath(scenario));

        var text = CsvCellEncoder.Cp932.GetString(bytes);
        text.Should().EndWith("\r\n");
        text.Replace("\r\n", string.Empty, StringComparison.Ordinal)
            .Should().NotContain("\n").And.NotContain("\r");
        CsvCellEncoder.Cp932.GetBytes(text).Should().Equal(bytes);
    }

    // 康熙部首（U+2F00 ブロック）は CP932 に無いため、氏名カナに混入したら fail-close する。
    // 「読めない字が空欄で通る」ことを防ぐ歯のあるテスト。
    [Fact]
    public void A_kangxi_radical_in_a_recipient_name_fails_closed_with_the_field_id()
    {
        var dto = ClaimCsvFixtures.Normal() with
        {
            Recipients = [ClaimCsvFixtures.Recipient("1234567890", kanaName: "⼀ﾉｾ ﾀﾛｳ")],
        };

        var act = () => new ClaimCsvGenerator(Catalog).Generate(dto);

        var exception = act.Should().Throw<ClaimCsvExportFailedException>().Which;
        exception.Reason.Should().Be("NonRepresentableCharacter");
        exception.FieldId.Should().StartWith("provider:J121:01:");
        // 例外に氏名そのものを載せない（CLAUDE.md §ハード制約4）。
        exception.Message.Should().NotContain("ﾀﾛｳ");
        exception.Detail.Should().NotContain("ﾀﾛｳ");
    }

    private static ClaimCsvDto Build(string scenario) => scenario switch
    {
        "normal" => ClaimCsvFixtures.Normal(),
        // 訂正請求は同じ確定内容を別 revision として再出力する。CSV 上の差は
        // 処理対象年月だけであり、明細の並びとバイト構造は通常請求と同じ規則で決まる。
        "correction" => ClaimCsvFixtures.Normal() with
        {
            ProcessingMonth = new Domain.ValueObjects.ProcessingMonth(2026, 9),
        },
        // 全角カナ氏名。CP932 で表現でき、引用規則（カンマ/引用符/空白/漢字）には該当しないため
        // 引用符なしで出力される。この解釈が変わればバイトが変わり golden が RED になる。
        "cjk" => ClaimCsvFixtures.Normal() with
        {
            Recipients = [ClaimCsvFixtures.Recipient("1234567890", kanaName: "ツムギタロウ")],
        },
        // NOTE(teeth): 受給者 2 名。請求書の集計行が受給者ごとの明細項目を参照する経路
        // （`fieldNonZero(provider:J121:01:031)` 等）は、受給者が 1 名のときだけ
        // 「候補が 1 行なら採用」のフォールバックで偶然通っていた。複数名を常設で固定する。
        "multi" => ClaimCsvFixtures.Normal() with
        {
            Recipients =
            [
                ClaimCsvFixtures.Recipient("1000000001"),
                ClaimCsvFixtures.Recipient("1000000002", kanaName: "ﾂﾑｷﾞ ﾊﾅｺ"),
            ],
        },
        _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
    };

    private static string GoldenPath(string scenario) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", $"csv-golden-{scenario}.csv");
}
