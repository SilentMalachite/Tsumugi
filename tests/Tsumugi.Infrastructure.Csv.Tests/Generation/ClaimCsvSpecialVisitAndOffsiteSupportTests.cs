using System.Globalization;
using FluentAssertions;
using Tsumugi.Application.Claim;
using Tsumugi.Application.Dtos.Claim.Csv;
using Tsumugi.Infrastructure.Csv.Generation;
using Tsumugi.Infrastructure.Csv.Specifications;
using Tsumugi.Infrastructure.Csv.Writer;

namespace Tsumugi.Infrastructure.Csv.Tests.Generation;

/// <summary>
/// 訪問支援特別加算と施設外支援の 4 項目（事業所編 基本情報 51〜54 / 日ごと明細情報 27〜28）。
/// 「サービス提供の実績」と「算定した回数・時間」は公式に別項目であり、後者は日次実績から
/// 導出できない（ADR 0033）。ここではその区別と、項目27の単位（1/100 時間）を固定する。
/// </summary>
public sealed class ClaimCsvSpecialVisitAndOffsiteSupportTests
{
    private static readonly CsvSpecificationCatalog Catalog = CsvSpecificationLoader.LoadEmbedded();

    // NOTE(teeth): 事業所編 日ごと明細情報 項目27 は「実際にサービス提供した時間数（時間）を
    // 整数部 2 桁・小数部 2 桁で設定」（例: 1.5 時間 → 0150）と定める。分をそのまま数値で出す
    // 実装へ戻すと、90 分が「90」＝0.90 時間として読まれる。
    [Theory]
    [InlineData(90, "150")]
    [InlineData(60, "100")]
    [InlineData(45, "75")]
    public void The_service_time_is_written_in_hundredths_of_an_hour(int minutes, string expected)
    {
        var cells = DailyCells(SingleDay(specialVisitSupportMinutes: minutes));

        cells[TokenIndexOf("provider:J611:02:027")].Should().Be(expected);
        cells[TokenIndexOf("provider:J611:02:027")].Should()
            .NotBe(minutes.ToString(CultureInfo.InvariantCulture), "the official unit is hours, not minutes");
    }

    // NOTE(teeth): 1/100 時間で表せない分（3 の倍数でない分）は、公式資料が丸め方向も丸め桁も
    // 定めていないため黙って丸めない。既定の丸めを入れると、加算の算定時間が根拠なく動く。
    [Fact]
    public void Minutes_without_an_exact_hundredths_of_an_hour_value_fail_closed()
    {
        var act = () => new ClaimCsvGenerator(Catalog)
            .Generate(SingleDay(specialVisitSupportMinutes: 50));

        var exception = act.Should().Throw<ClaimCsvExportFailedException>().Which;
        exception.Reason.Should().Be(nameof(ClaimCsvGenerationReason.UnresolvableRule));
        exception.FieldId.Should().Be("provider:J611:02:027");
    }

    // NOTE(teeth): 算定時間数（項目28）は専用の日次個別入力から出す。項目27（分の実績）を
    // 流用すると、留意事項通知 2(6)⑨ の「実際に要した時間により算定されるのではない」に反する。
    [Fact]
    public void The_billed_hours_come_from_their_own_daily_input()
    {
        var cells = DailyCells(SingleDay(
            specialVisitSupportMinutes: 90, specialVisitSupportBilledHours: 2));

        cells[TokenIndexOf("provider:J611:02:028")].Should().Be("2");
        // 提供時間（1/100 時間）と算定時間（時間）が別の値として並存する。
        cells[TokenIndexOf("provider:J611:02:027")].Should().Be("150");
    }

    [Fact]
    public void The_billed_hours_are_blank_when_the_snapshot_carries_none()
    {
        // 個別入力が未供給のときに実績値で埋めない（空欄）。必須化は readiness 側の要件
        // （ClaimInputRequirementProvider）が担い、生成側は値を作らない。
        var cells = DailyCells(SingleDay(specialVisitSupportMinutes: 90));

        cells[TokenIndexOf("provider:J611:02:028")].Should().BeEmpty();
    }

    // NOTE(teeth): 基本情報 項目51（サービス提供回数）は日次実績から導出できるが、
    // 項目52（算定回数）は導出できないため月次個別入力から出す。両者を同じ値にする実装へ
    // 戻すと、1 月に 2 回算定した月（再度 5 日間以上連続して利用がなかった場合）を表せない。
    [Fact]
    public void The_monthly_billed_count_comes_from_the_claim_input_and_differs_from_the_service_count()
    {
        var cells = BasicCells(ClaimCsvFixtures.Normal());

        cells[TokenIndexOf("provider:J611:01:051")].Should().Be("1", "one day has a visit record");
        cells[TokenIndexOf("provider:J611:01:052")].Should().Be("2", "the billed count is entered");
    }

    // NOTE(teeth): 項目54 は年度累計（毎年 4/1〜翌 3/31 の実利用日数合計）であり、当月分
    // （項目53）とは別の値。当月分で代用すると、180 日の上限管理が壊れる。
    [Fact]
    public void The_cumulative_offsite_days_come_from_the_claim_input_and_differ_from_the_month()
    {
        var cells = BasicCells(ClaimCsvFixtures.Normal());

        cells[TokenIndexOf("provider:J611:01:053")].Should().Be("1", "one day is an offsite support day");
        cells[TokenIndexOf("provider:J611:01:054")].Should().Be("12", "the cumulative total is entered");
    }

    [Theory]
    [InlineData("provider:J611:01:052")]
    [InlineData("provider:J611:01:054")]
    public void The_monthly_inputs_are_blank_when_the_snapshot_carries_none(string fieldId)
    {
        var dto = ClaimCsvFixtures.Normal() with
        {
            Recipients =
            [
                ClaimCsvFixtures.Recipient(
                    "1234567890",
                    specialVisitSupportBilledCount: null,
                    offsiteSupportCumulativeDays: null),
            ],
        };

        BasicCells(dto)[TokenIndexOf(fieldId)].Should().BeEmpty();
    }

    private static ClaimCsvDto SingleDay(
        int? specialVisitSupportMinutes = null,
        int? specialVisitSupportBilledHours = null) =>
        ClaimCsvFixtures.Normal() with
        {
            Recipients =
            [
                ClaimCsvFixtures.Recipient("1234567890") with
                {
                    DailyRecords =
                    [
                        ClaimCsvFixtures.Day(
                            new DateOnly(2026, 7, 1),
                            specialVisitSupportMinutes: specialVisitSupportMinutes,
                            specialVisitSupportBilledHours: specialVisitSupportBilledHours),
                    ],
                },
            ],
        };

    private static string[] DailyCells(ClaimCsvDto dto) => Rows(dto, "J611", "02").Single();

    private static string[] BasicCells(ClaimCsvDto dto) => Rows(dto, "J611", "01").Single();

    private static IEnumerable<string[]> Rows(ClaimCsvDto dto, string exchangeId, string recordType) =>
        CsvCellEncoder.Cp932.GetString(new ClaimCsvGenerator(Catalog).Generate(dto).Bytes)
            .Split("\r\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split(','))
            .Where(parts => parts.Length > 3 && parts[2] == exchangeId && parts[3] == recordType);

    private static int TokenIndexOf(string fieldId) =>
        Catalog.ProviderRecords
            .SelectMany(record => record.Fields)
            .Single(field => field.FieldId == fieldId)
            .Position + 1;
}
