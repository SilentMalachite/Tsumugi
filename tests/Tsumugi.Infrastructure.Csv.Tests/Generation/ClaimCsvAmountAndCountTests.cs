using FluentAssertions;
using Tsumugi.Application.Dtos.Claim.Csv;
using Tsumugi.Infrastructure.Csv.Generation;
using Tsumugi.Infrastructure.Csv.Specifications;
using Tsumugi.Infrastructure.Csv.Writer;

namespace Tsumugi.Infrastructure.Csv.Tests.Generation;

/// <summary>
/// 金額・回数の境界。Phase 3-3 コードレビューで見つかった 2 件の欠陥を固定する。
/// </summary>
public sealed class ClaimCsvAmountAndCountTests
{
    private static readonly CsvSpecificationCatalog Catalog = CsvSpecificationLoader.LoadEmbedded();

    private static string[] Tokens(ClaimCsvDto dto, string exchangeId, string recordType) =>
        CsvCellEncoder.Cp932.GetString(new ClaimCsvGenerator(Catalog).Generate(dto))
            .Split("\r\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split(','))
            .Single(parts => parts.Length > 3 && parts[2] == exchangeId && parts[3] == recordType);

    private static int TokenIndexOf(string fieldId) =>
        Catalog.ProviderRecords
            .SelectMany(record => record.Fields)
            .Single(field => field.FieldId == fieldId)
            .Position + 1;

    // NOTE(teeth): 負担上限が 0 円（生活保護等）の利用者では、調整後利用者負担額
    // provider:J121:04:019 が「0 なら出力しない」自己参照条件で空欄になる。それを参照する
    // 決定利用者負担額 provider:J121:04:021 は always 必須なので、値まで欠測扱いにすると
    // CSV 生成そのものが落ちる。0 は空欄ではなく "0" として出力されなければならない。
    [Fact]
    public void A_zero_burden_recipient_still_produces_a_decided_burden_amount()
    {
        var dto = ZeroBurden();

        var aggregate = Tokens(dto, "J121", "04");

        // 調整後利用者負担額は 0 なので空欄。
        aggregate[TokenIndexOf("provider:J121:04:019")].Should().BeEmpty();
        // 決定利用者負担額は必須。0 円でも "0" を出す。
        aggregate[TokenIndexOf("provider:J121:04:021")].Should().Be("0");
        // 請求額（給付費）は総費用額そのまま。
        aggregate[TokenIndexOf("provider:J121:04:022")].Should().Be("30000");
    }

    [Fact]
    public void A_zero_burden_recipient_does_not_fail_the_export()
    {
        var act = () => new ClaimCsvGenerator(Catalog).Generate(ZeroBurden());

        act.Should().NotThrow();
    }

    // NOTE(teeth): 送迎加算の実績は片道換算（ADR 0028 決定5）。往復は 1 日でも 2 回。
    // 日数で数えると往復日が過少になる。
    [Theory]
    [InlineData(0, "")]      // 送迎なし
    [InlineData(1, "1")]     // 往のみ
    [InlineData(2, "1")]     // 復のみ
    [InlineData(3, "2")]     // 往復
    public void Transport_is_counted_as_one_way_trips(int transportCode, string expected)
    {
        var dto = SingleDay(transportCode);

        var monthly = Tokens(dto, "J611", "01");

        monthly[TokenIndexOf("provider:J611:01:034")].Should().Be(expected);
    }

    // NOTE(teeth): 月次の送迎回数は、日次レコードの「往」「復」欄の合計と一致する。
    [Fact]
    public void The_monthly_transport_count_equals_the_sum_of_the_daily_outbound_and_inbound_cells()
    {
        var dto = ClaimCsvFixtures.Normal() with
        {
            Recipients =
            [
                ClaimCsvFixtures.Recipient("1234567890") with
                {
                    DailyRecords =
                    [
                        ClaimCsvFixtures.Day(new DateOnly(2026, 7, 1), transportCode: 1),
                        ClaimCsvFixtures.Day(new DateOnly(2026, 7, 2), transportCode: 3),
                        ClaimCsvFixtures.Day(new DateOnly(2026, 7, 3), transportCode: 2),
                        ClaimCsvFixtures.Day(new DateOnly(2026, 7, 6), transportCode: 0),
                    ],
                },
            ],
        };

        var lines = CsvCellEncoder.Cp932.GetString(new ClaimCsvGenerator(Catalog).Generate(dto))
            .Split("\r\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split(','))
            .ToArray();

        var outbound = TokenIndexOf("provider:J611:02:021");
        var inbound = TokenIndexOf("provider:J611:02:022");
        var dailyTotal = lines
            .Where(parts => parts.Length > 3 && parts[2] == "J611" && parts[3] == "02")
            .Sum(parts => Count(parts[outbound]) + Count(parts[inbound]));

        var monthly = lines.Single(parts => parts.Length > 3 && parts[2] == "J611" && parts[3] == "01");
        monthly[TokenIndexOf("provider:J611:01:034")].Should().Be(dailyTotal.ToString(
            System.Globalization.CultureInfo.InvariantCulture));
        dailyTotal.Should().Be(4); // 往1 + 往復2 + 復1
    }

    private static int Count(string cell) =>
        string.IsNullOrEmpty(cell) ? 0 : int.Parse(cell, System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>負担上限 0 円（生活保護等）の利用者。</summary>
    private static ClaimCsvDto ZeroBurden() => ClaimCsvFixtures.Normal() with
    {
        Recipients = [ClaimCsvFixtures.Recipient("1234567890") with { MonthlyCostCapYen = 0 }],
    };

    private static ClaimCsvDto SingleDay(int transportCode) => ClaimCsvFixtures.Normal() with
    {
        Recipients =
        [
            ClaimCsvFixtures.Recipient("1234567890") with
            {
                DailyRecords = [ClaimCsvFixtures.Day(new DateOnly(2026, 7, 1), transportCode: transportCode)],
            },
        ],
    };
}
