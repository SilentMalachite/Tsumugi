using FluentAssertions;
using Tsumugi.Infrastructure.Csv.Generation;
using Tsumugi.Infrastructure.Csv.Specifications;
using Tsumugi.Infrastructure.Csv.Writer;

namespace Tsumugi.Infrastructure.Csv.Tests.Generation;

public sealed class ClaimCsvGeneratorTests
{
    private static readonly CsvSpecificationCatalog Catalog = CsvSpecificationLoader.LoadEmbedded();

    private static string[] Lines(byte[] bytes) =>
        CsvCellEncoder.Cp932.GetString(bytes).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

    [Fact]
    public void Generate_writes_the_outer_three_record_frame()
    {
        var bytes = new ClaimCsvGenerator(Catalog).Generate(ClaimCsvFixtures.Normal());

        var lines = Lines(bytes);
        lines[0].Should().StartWith("1,");
        lines[1..^1].Should().OnlyContain(line => line.StartsWith("2,", StringComparison.Ordinal));
        lines[^1].Should().StartWith("3,");
        CsvCellEncoder.Cp932.GetString(bytes).Should().EndWith("\r\n");
    }

    // AC3-7: 独立入力の ProcessingMonth がコントロールレコードへ入る。
    [Fact]
    public void Generate_writes_the_processing_month_into_the_control_record()
    {
        var bytes = new ClaimCsvGenerator(Catalog).Generate(ClaimCsvFixtures.Normal());

        var control = Lines(bytes)[0].Split(',');
        var processingMonthPosition = Catalog.CommonRecords
            .Single(record => record.RecordId == "common:outer:control")
            .Fields.Single(field => field.FieldId == "common:outer:control:010")
            .Position;

        control[processingMonthPosition - 1].Should().Be("202608");
        // サービス提供年月（202607）とは別の値であることを固定する。
        control[processingMonthPosition - 1].Should().NotBe("202607");
    }

    [Fact]
    public void Generate_numbers_records_from_one_and_ends_at_data_count_plus_two()
    {
        var bytes = new ClaimCsvGenerator(Catalog).Generate(ClaimCsvFixtures.Normal());

        var lines = Lines(bytes);
        var dataCount = lines.Length - 2;
        lines[0].Split(',')[1].Should().Be("1");
        lines[1].Split(',')[1].Should().Be("2");
        lines[^1].Split(',')[1].Should().Be((dataCount + 2).ToString(System.Globalization.CultureInfo.InvariantCulture));
        lines[0].Split(',')[3].Should().Be(dataCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    // AC3-7: 公式の内側レコード順（provider-claim-r7-10.json の order）。
    [Fact]
    public void Generate_emits_inner_records_in_the_official_record_order()
    {
        var bytes = new ClaimCsvGenerator(Catalog).Generate(ClaimCsvFixtures.Normal());

        var exchangeIds = Lines(bytes)[1..^1]
            .Select(line => line.Split(',')[2].Trim('"'))
            .ToArray();
        var recordTypes = Lines(bytes)[1..^1]
            .Select(line => line.Split(',')[3].Trim('"'))
            .ToArray();

        exchangeIds.Should().ContainInOrder("J111", "J111", "J121", "J121", "J121", "J121", "J611", "J611");
        exchangeIds.Should().NotContain("J121:05");
        recordTypes[0].Should().Be("01");
        recordTypes[1].Should().Be("02");
    }

    [Fact]
    public void Generate_derives_the_data_kind_from_the_first_inner_exchange_information_id()
    {
        var bytes = new ClaimCsvGenerator(Catalog).Generate(ClaimCsvFixtures.Normal());

        Lines(bytes)[0].Split(',')[4].Should().Be("J11");
    }

    [Fact]
    public void Generate_writes_the_office_number_into_the_control_record()
    {
        var bytes = new ClaimCsvGenerator(Catalog).Generate(ClaimCsvFixtures.Normal());

        Lines(bytes)[0].Split(',')[6].Should().Be("1312345678");
    }

    [Fact]
    public void Generate_is_deterministic_for_the_same_input()
    {
        var generator = new ClaimCsvGenerator(Catalog);

        var first = generator.Generate(ClaimCsvFixtures.Normal());
        var second = generator.Generate(ClaimCsvFixtures.Normal());

        second.Should().Equal(first);
    }

    [Fact]
    public void Generate_emits_one_service_line_record_per_claim_line()
    {
        var dto = ClaimCsvFixtures.Normal();

        var lines = Lines(new ClaimCsvGenerator(Catalog).Generate(dto));

        var serviceLineRecords = lines[1..^1]
            .Where(line => line.Split(',')[2].Trim('"') == "J121" && line.Split(',')[3].Trim('"') == "03")
            .ToArray();
        serviceLineRecords.Should().HaveCount(dto.Recipients.Sum(r => r.ServiceLines.Count));
    }

    [Fact]
    public void Generate_emits_one_daily_record_per_service_day()
    {
        var dto = ClaimCsvFixtures.Normal();

        var lines = Lines(new ClaimCsvGenerator(Catalog).Generate(dto));

        var dailyRecords = lines[1..^1]
            .Where(line => line.Split(',')[2].Trim('"') == "J611" && line.Split(',')[3].Trim('"') == "02")
            .ToArray();
        dailyRecords.Should().HaveCount(dto.Recipients.Sum(r => r.DailyRecords.Count));
    }

    [Fact]
    public void Generate_fails_closed_when_there_is_no_recipient()
    {
        var dto = ClaimCsvFixtures.Normal() with { Recipients = [] };

        var act = () => new ClaimCsvGenerator(Catalog).Generate(dto);

        act.Should().Throw<Exception>();
    }
}
