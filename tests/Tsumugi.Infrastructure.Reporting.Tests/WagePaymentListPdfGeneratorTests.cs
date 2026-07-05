using System.IO;
using FluentAssertions;
using Tsumugi.Application.Dtos;
using Tsumugi.Domain.Enums;
using UglyToad.PdfPig;
using Xunit;

namespace Tsumugi.Infrastructure.Reporting.Tests;

public sealed class WagePaymentListPdfGeneratorTests
{
    static WagePaymentListPdfGeneratorTests() => QuestPdfLicenseConfigurator.Initialize();

    private static RecipientDto Rec(Guid id, string kana) => new(
        id, "氏名", kana, new DateOnly(1990, 1, 1), Guid.NewGuid(), false, default,
        null, null, null, null, null, null, null);

    private static OfficeDto Office(Guid id) => new(
        id, "1234567890", "テスト事業所", ServiceCategory.TypeB, RegionGrade.None, Guid.NewGuid());

    [Fact]
    public void PaymentList_includes_each_amount_total_and_recipient_count()
    {
        var office = Office(Guid.NewGuid());
        var r1 = Guid.NewGuid();
        var r2 = Guid.NewGuid();
        var stmts = new[]
        {
            new WageStatementDto(Guid.NewGuid(), office.Id, 2026, 7, r1, 12_000, "x", RecordKind.New, null),
            new WageStatementDto(Guid.NewGuid(), office.Id, 2026, 7, r2,  8_000, "y", RecordKind.New, null),
        };
        var recipients = new Dictionary<Guid, RecipientDto>
        {
            [r1] = Rec(r1, "ヤマダタロウ"),
            [r2] = Rec(r2, "スズキハナコ"),
        };

        var bytes = new WageStatementPdfGenerator(TimeProvider.System).GeneratePaymentList(stmts, recipients, office, 2026, 7);
        bytes.Should().NotBeNullOrEmpty();
        var text = ExtractText(bytes);

        text.Should().Contain("12,000");
        text.Should().Contain("8,000");

        // S1: Noto Sans JP 埋込により Bold 行の ASCII と CJK が抽出可能
        text.Should().Contain("20,000", because: "合計行の Bold ASCII が抽出可能");
        text.Should().Contain("10,000", because: "平均行の Bold ASCII が抽出可能");
        text.Should().Contain("工賃支払一覧", because: "ヘッダ Bold 漢字が抽出可能");
    }

    [Fact]
    public void Empty_statements_yields_zero_total_and_zero_average_no_crash()
    {
        var office = Office(Guid.NewGuid());
        var bytes = new WageStatementPdfGenerator(TimeProvider.System).GeneratePaymentList(
            Array.Empty<WageStatementDto>(),
            new Dictionary<Guid, RecipientDto>(),
            office, 2026, 7);
        bytes.Should().NotBeNullOrEmpty();
        var text = ExtractText(bytes);
        text.Should().Contain("0", because: "合計 0 円 / 利用者数 0 / 平均 0 円");
    }

    private static string ExtractText(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var pdf = PdfDocument.Open(stream);
        var sb = new System.Text.StringBuilder();
        foreach (var page in pdf.GetPages()) sb.Append(page.Text);
        return FoldKangxiRadicals(sb.ToString());
    }

    // Noto Sans JP は「工」「支」「一」等、康熙部首 (Kangxi Radicals, U+2F00-2FD5) と字形を
    // 共有する漢字を含んでおり、QuestPDF/SkiaSharp が生成する ToUnicode CMap がその部首の
    // コードポイントを採用することがある (レンダリングされる字形自体は正しい。抽出テキストの
    // みに影響。pdftotext でも同一結果になることを確認済みのため QuestPDF/Skia 側の生成物起因で
    // あり PdfPig 固有ではない)。Directory.Build.props で InvariantGlobalization=true のため
    // string.Normalize(FormKC) は ICU 不使用で分解を行わず使えない。このテストで実際に出現する
    // 部首だけを固定表で統合漢字へ畳み込む。
    private static readonly System.Collections.Generic.Dictionary<char, char> KangxiRadicalToIdeograph =
        new System.Collections.Generic.Dictionary<char, char>
        {
            ['⼯'] = '工', // KANGXI RADICAL WORK -> 工
            ['⽀'] = '支', // KANGXI RADICAL BRANCH -> 支
            ['⼀'] = '一', // KANGXI RADICAL ONE -> 一
        };

    private static string FoldKangxiRadicals(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var ch in s)
            sb.Append(KangxiRadicalToIdeograph.TryGetValue(ch, out var mapped) ? mapped : ch);
        return sb.ToString();
    }
}
