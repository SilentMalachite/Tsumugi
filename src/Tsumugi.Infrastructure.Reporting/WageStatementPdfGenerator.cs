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
                    t.Span(DateTime.UtcNow.ToString("yyyy-MM-dd"));
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
        int month) => throw new NotImplementedException("Task E3 で実装する。");
}
