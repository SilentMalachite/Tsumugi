using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;

namespace Tsumugi.Infrastructure.Reporting;

/// <summary>
/// 工賃帳票 PDF 生成。
/// ADR 0013 により QuestPDF Community ライセンスで動作する前提。
/// テストは抽出テキストと合計金額の一致で検証する（バイト一致は環境差で揺れるため避ける）。
/// </summary>
public sealed class WageStatementPdfGenerator : IWageReportGenerator
{
    public byte[] GenerateStatement(WageStatementDto statement, RecipientDto recipient, OfficeDto office)
    {
        ArgumentNullException.ThrowIfNull(statement);
        ArgumentNullException.ThrowIfNull(recipient);
        ArgumentNullException.ThrowIfNull(office);

        var doc = Document.Create(c =>
        {
            c.Page(p =>
            {
                p.Size(PageSizes.A4);
                p.Margin(2, Unit.Centimetre);
                p.Header().Text($"{office.Name}　{statement.Year}年{statement.Month}月分 工賃明細")
                    .FontSize(14).Bold();
                p.Content().Column(col =>
                {
                    col.Spacing(8);
                    col.Item().Text($"利用者: {recipient.KanjiName}（{recipient.KanaName}）");
                    col.Item().Text($"金額: {statement.AmountYen:N0} 円");
                    col.Item().Text($"算定根拠: {statement.BasisSummary}");
                });
                p.Footer().AlignCenter().Text(t =>
                {
                    t.Span("発行日: ");
                    t.Span(DateTime.UtcNow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
                });
            });
        });
        return doc.GeneratePdf();
    }

    public byte[] GeneratePaymentList(
        IReadOnlyList<WageStatementDto> statements,
        IReadOnlyDictionary<Guid, RecipientDto> recipients,
        OfficeDto office,
        int year,
        int month)
    {
        ArgumentNullException.ThrowIfNull(statements);
        ArgumentNullException.ThrowIfNull(recipients);
        ArgumentNullException.ThrowIfNull(office);

        var total = statements.Sum(s => s.AmountYen);
        var count = statements.Count;
        var average = count == 0 ? 0 : total / count;

        var doc = Document.Create(c =>
        {
            c.Page(p =>
            {
                p.Size(PageSizes.A4);
                p.Margin(2, Unit.Centimetre);
                p.Header().Text($"{office.Name}　{year}年{month}月分 工賃支払一覧")
                    .FontSize(14).Bold();
                p.Content().Column(col =>
                {
                    col.Spacing(8);
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(cd =>
                        {
                            cd.RelativeColumn(3);
                            cd.RelativeColumn(2);
                        });
                        t.Header(h =>
                        {
                            h.Cell().Text("利用者").Bold();
                            h.Cell().AlignRight().Text("金額（円）").Bold();
                        });
                        foreach (var s in statements)
                        {
                            var name = recipients.TryGetValue(s.RecipientId, out var r)
                                ? $"{r.KanjiName}（{r.KanaName}）"
                                : s.RecipientId.ToString();
                            t.Cell().Text(name);
                            t.Cell().AlignRight().Text($"{s.AmountYen:N0}");
                        }
                    });
                    col.Item().AlignRight().Text($"合計: {total:N0} 円　/　利用者数: {count}　/　平均: {average:N0} 円")
                        .Bold();
                });
                p.Footer().AlignCenter().Text(t =>
                {
                    t.Span("発行日: ");
                    t.Span(DateTime.UtcNow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
                });
            });
        });
        return doc.GeneratePdf();
    }
}
