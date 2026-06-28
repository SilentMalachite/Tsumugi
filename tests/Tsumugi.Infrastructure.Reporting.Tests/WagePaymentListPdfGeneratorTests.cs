using System.IO;
using FluentAssertions;
using Tsumugi.Application.Dtos;
using Tsumugi.Domain.Enums;
using UglyToad.PdfPig;
using Xunit;

namespace Tsumugi.Infrastructure.Reporting.Tests;

public sealed class WagePaymentListPdfGeneratorTests
{
    static WagePaymentListPdfGeneratorTests() => QuestPdfLicenseConfigurator.ApplyCommunityLicense();

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

        text.Should().Contain("ヤマダタロウ");
        text.Should().Contain("スズキハナコ");
        text.Should().Contain("12,000");
        text.Should().Contain("8,000");
        text.Should().Contain("20,000", because: "合計金額");
        text.Should().Contain("10,000", because: "平均額 = 20,000 / 2");
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
        return sb.ToString();
    }
}
