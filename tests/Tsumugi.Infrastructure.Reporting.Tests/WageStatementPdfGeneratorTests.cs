using System.IO;
using FluentAssertions;
using Tsumugi.Application.Dtos;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;
using UglyToad.PdfPig;
using Xunit;

namespace Tsumugi.Infrastructure.Reporting.Tests;

public sealed class WageStatementPdfGeneratorTests
{
    static WageStatementPdfGeneratorTests() => QuestPdfLicenseConfigurator.Initialize();

    [Fact]
    public void Statement_pdf_contains_recipient_name_office_name_and_amount()
    {
        var stmt = new WageStatementDto(
            Id: Guid.NewGuid(),
            OfficeId: Guid.NewGuid(),
            Year: 2026,
            Month: 7,
            RecipientId: Guid.NewGuid(),
            AmountYen: 12_345,
            BasisSummary: "時間割: 600分 / 原資100,000円",
            Kind: RecordKind.New,
            OriginId: null);

        var recipient = new RecipientDto(
            stmt.RecipientId, "山田太郎", "ヤマダタロウ",
            new DateOnly(1970, 1, 1), Guid.NewGuid(), IsArchived: false,
            Disabilities: default, PostalCode: null, Address: null,
            PhoneNumber: null, EmailAddress: null,
            EmergencyContactName: null, EmergencyContactRelationship: null, EmergencyContactPhone: null);

        var office = new OfficeDto(
            stmt.OfficeId, "1234567890", "テスト事業所",
            ServiceCategory.TypeB, RegionGrade.None, Guid.NewGuid());

        var bytes = new WageStatementPdfGenerator(TimeProvider.System).GenerateStatement(stmt, recipient, office);
        bytes.Should().NotBeNullOrEmpty();

        var text = ExtractText(bytes);
        text.Should().Contain("12,345", because: "金額は N0 形式で桁区切り");
        text.Should().Contain("2026", because: "対象年がヘッダに出る");

        // S1: Noto Sans JP 埋込により CJK が抽出可能
        text.Should().Contain("山田太郎", because: "利用者名 (漢字) がフォント埋込で抽出可能");
        text.Should().Contain("ヤマダタロウ", because: "カナが抽出可能");
        text.Should().Contain("テスト事業所", because: "事業所名 (ひらがな含む漢字) が抽出可能");
        text.Should().Contain("工賃明細", because: "帳票タイトル (漢字) がヘッダ Bold で抽出可能");
    }

    [Fact]
    public void Generate_rejects_null_arguments()
    {
        var gen = new WageStatementPdfGenerator(TimeProvider.System);
        var stmt = new WageStatementDto(Guid.NewGuid(), Guid.NewGuid(), 2026, 7, Guid.NewGuid(),
            1000, "x", RecordKind.New, null);
        var r = new RecipientDto(stmt.RecipientId, "k", "K", new DateOnly(1990, 1, 1),
            Guid.NewGuid(), false, default, null, null, null, null, null, null, null);
        var o = new OfficeDto(stmt.OfficeId, "0", "n", ServiceCategory.TypeB, RegionGrade.None, Guid.NewGuid());

        FluentActions.Invoking(() => gen.GenerateStatement(null!, r, o)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => gen.GenerateStatement(stmt, null!, o)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => gen.GenerateStatement(stmt, r, null!)).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Statement_pdf_is_deterministic_for_same_inputs_and_same_timeprovider()
    {
        var fixedTime = new DateTimeOffset(2026, 7, 15, 10, 0, 0, TimeSpan.Zero);
        var tp = new FixedTimeProvider(fixedTime);
        var gen = new WageStatementPdfGenerator(tp);

        var recipientId = Guid.NewGuid();
        var officeId = Guid.NewGuid();
        var stmt = new WageStatementDto(
            Guid.NewGuid(), officeId, 2026, 7, recipientId,
            50_000, "時間割方式: 600分 / 原資100,000円", RecordKind.New, null);
        var recipient = new RecipientDto(
            recipientId, "氏名", "シメイ", new DateOnly(1990, 1, 1), Guid.NewGuid(), false,
            default, null, null, null, null, null, null, null);
        var office = new OfficeDto(
            officeId, "1234567890", "事業所",
            ServiceCategory.TypeB, RegionGrade.None, Guid.NewGuid());

        var a = gen.GenerateStatement(stmt, recipient, office);
        var b = gen.GenerateStatement(stmt, recipient, office);

        a.Should().BeEquivalentTo(b, "同一入力＋同一 TimeProvider なら出力 PDF は決定論的に同一");
    }

    private static string ExtractText(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var pdf = PdfDocument.Open(stream);
        var sb = new System.Text.StringBuilder();
        foreach (var page in pdf.GetPages())
            sb.Append(page.Text);
        return FoldKangxiRadicals(sb.ToString());
    }

    // Noto Sans JP は「山」「田」「工」等、康熙部首 (Kangxi Radicals, U+2F00-2FD5) と字形を
    // 共有する漢字を含んでおり、QuestPDF/SkiaSharp が生成する ToUnicode CMap がその部首の
    // コードポイントを採用することがある (レンダリングされる字形自体は正しい。抽出テキストの
    // みに影響。pdftotext でも同一結果になることを確認済みのため QuestPDF/Skia 側の生成物起因で
    // あり PdfPig 固有ではない)。Directory.Build.props で InvariantGlobalization=true のため
    // string.Normalize(FormKC) は ICU 不使用で分解を行わず使えない。このテストで実際に出現する
    // 部首だけを固定表で統合漢字へ畳み込む。
    private static readonly System.Collections.Generic.Dictionary<char, char> KangxiRadicalToIdeograph =
        new System.Collections.Generic.Dictionary<char, char>
        {
            ['⼭'] = '山', // KANGXI RADICAL MOUNTAIN -> 山
            ['⽥'] = '田', // KANGXI RADICAL FIELD -> 田
            ['⼯'] = '工', // KANGXI RADICAL WORK -> 工
        };

    private static string FoldKangxiRadicals(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var ch in s)
            sb.Append(KangxiRadicalToIdeograph.TryGetValue(ch, out var mapped) ? mapped : ch);
        return sb.ToString();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
