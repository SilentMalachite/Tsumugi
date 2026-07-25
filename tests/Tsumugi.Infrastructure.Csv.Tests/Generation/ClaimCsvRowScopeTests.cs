using FluentAssertions;
using Tsumugi.Application.Dtos.Claim.Csv;
using Tsumugi.Infrastructure.Csv.Generation;
using Tsumugi.Infrastructure.Csv.Specifications;
using Tsumugi.Infrastructure.Csv.Writer;

namespace Tsumugi.Infrastructure.Csv.Tests.Generation;

/// <summary>
/// 集約規則は「対象行の RowKey を接頭辞に持つ行」を畳み込む。受給者スコープが隣の受給者へ
/// 漏れないこと、および受給者キーの接頭辞衝突が起きないことを固定する。
/// </summary>
public sealed class ClaimCsvRowScopeTests
{
    private static readonly CsvSpecificationCatalog Catalog = CsvSpecificationLoader.LoadEmbedded();

    // NOTE(teeth): 受給者キーが区切り文字で終わらないと "R1000" が "R10000" の接頭辞になり、
    // 受給者 1000 の集計が受給者 10000 の行を巻き込む。
    [Fact]
    public void A_recipient_scope_never_matches_a_recipient_whose_index_shares_its_prefix()
    {
        var narrow = ClaimCsvRowPlan.Recipient("provider:J121:01", 1000);
        var wide = ClaimCsvRowPlan.Recipient("provider:J121:01", 10000);

        wide.IsWithin(narrow.RowKey).Should().BeFalse();
        narrow.IsWithin(wide.RowKey).Should().BeFalse();
    }

    [Fact]
    public void Child_rows_stay_within_their_own_recipient_scope()
    {
        var recipient = ClaimCsvRowPlan.Recipient("provider:J121:01", 3);
        var ownLine = ClaimCsvRowPlan.ServiceLine("provider:J121:03", 3, 7);
        var ownDay = ClaimCsvRowPlan.DailyRecord("provider:J611:02", 3, 7);
        var otherLine = ClaimCsvRowPlan.ServiceLine("provider:J121:03", 4, 7);

        ownLine.IsWithin(recipient.RowKey).Should().BeTrue();
        ownDay.IsWithin(recipient.RowKey).Should().BeTrue();
        otherLine.IsWithin(recipient.RowKey).Should().BeFalse();
        recipient.IsWithin(ClaimCsvRowPlan.FileRowKey).Should().BeTrue();
    }

    // NOTE(teeth): 受給者ごとの集計が隣の受給者と混ざっていないことを、実 spec を通した
    // バイト列で確かめる。RowKey のスコープ判定が壊れるとここが RED になる。
    [Fact]
    public void Per_recipient_totals_do_not_leak_between_recipients()
    {
        var dto = TwoRecipients();

        var lines = CsvCellEncoder.Cp932
            .GetString(new ClaimCsvGenerator(Catalog).Generate(dto).Bytes)
            .Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        var aggregates = lines
            .Where(line => line.Split(',') is [_, _, "J121", "04", ..])
            .Select(line => line.Split(','))
            .ToArray();

        aggregates.Should().HaveCount(2);
        // 受給者証番号の昇順に並び、給付単位数はそれぞれ自分の明細だけを畳み込む。
        var certificateNumber = TokenIndexOf("provider:J121:04:006");
        var benefitUnits = TokenIndexOf("provider:J121:04:010");
        aggregates[0][certificateNumber].Should().Be("1000000001");
        aggregates[0][benefitUnits].Should().Be("1000");
        aggregates[1][certificateNumber].Should().Be("1000000002");
        aggregates[1][benefitUnits].Should().Be("2000");
    }

    [Fact]
    public void File_level_totals_fold_every_recipient()
    {
        var dto = TwoRecipients();

        var lines = CsvCellEncoder.Cp932
            .GetString(new ClaimCsvGenerator(Catalog).Generate(dto).Bytes)
            .Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        var invoiceTotals = lines.Single(line => line.Split(',') is [_, _, "J111", "02", ..]).Split(',');

        // 件数は明細書レコード数、合計単位数は全受給者の合計。
        invoiceTotals[TokenIndexOf("provider:J111:02:008")].Should().Be("2");
        invoiceTotals[TokenIndexOf("provider:J111:02:009")].Should().Be("3000");
    }

    /// <summary>
    /// 内側項目が外側データレコードの何番目のトークンに来るか。データレコードは
    /// 「レコード種別, レコード番号, 内側レコード…」の順なので、内側の 1 始まり位置に 1 を足す。
    /// </summary>
    private static int TokenIndexOf(string fieldId) =>
        Catalog.ProviderRecords
            .SelectMany(record => record.Fields)
            .Single(field => field.FieldId == fieldId)
            .Position + 1;

    private static ClaimCsvDto TwoRecipients()
    {
        var first = ClaimCsvFixtures.Recipient("1000000001") with
        {
            ServiceLines = [new ClaimCsvServiceLineDto("462980", Unit: 100, Count: 10)],
        };
        var second = ClaimCsvFixtures.Recipient("1000000002") with
        {
            ServiceLines = [new ClaimCsvServiceLineDto("462980", Unit: 200, Count: 10)],
        };
        return ClaimCsvFixtures.Normal() with { Recipients = [first, second] };
    }
}
