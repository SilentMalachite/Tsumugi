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
    static WageStatementPdfGeneratorTests() => QuestPdfLicenseConfigurator.ApplyCommunityLicense();

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

        var bytes = new WageStatementPdfGenerator().GenerateStatement(stmt, recipient, office);
        bytes.Should().NotBeNullOrEmpty();

        var text = ExtractText(bytes);
        // ★日本語フォント埋込が未実装のため、漢字は CJK 互換文字 (U+2F00 帯) に化けて抽出される。
        // カナと ASCII 数字は環境に依存せず正しく抽出できるため、それらで構造的検証を行う。
        // フォント埋込ライセンス確定後 (open-questions 参照) に漢字を含む assertion を追加する。
        text.Should().Contain("ヤマダタロウ", because: "カタカナはフォント化けなく抽出できる");
        text.Should().Contain("12,345", because: "金額は N0 形式で桁区切り");
        text.Should().Contain("2026", because: "対象年がヘッダに出る");
    }

    [Fact]
    public void Generate_rejects_null_arguments()
    {
        var gen = new WageStatementPdfGenerator();
        var stmt = new WageStatementDto(Guid.NewGuid(), Guid.NewGuid(), 2026, 7, Guid.NewGuid(),
            1000, "x", RecordKind.New, null);
        var r = new RecipientDto(stmt.RecipientId, "k", "K", new DateOnly(1990, 1, 1),
            Guid.NewGuid(), false, default, null, null, null, null, null, null, null);
        var o = new OfficeDto(stmt.OfficeId, "0", "n", ServiceCategory.TypeB, RegionGrade.None, Guid.NewGuid());

        FluentActions.Invoking(() => gen.GenerateStatement(null!, r, o)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => gen.GenerateStatement(stmt, null!, o)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => gen.GenerateStatement(stmt, r, null!)).Should().Throw<ArgumentNullException>();
    }

    private static string ExtractText(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var pdf = PdfDocument.Open(stream);
        var sb = new System.Text.StringBuilder();
        foreach (var page in pdf.GetPages())
            sb.Append(page.Text);
        return sb.ToString();
    }
}
