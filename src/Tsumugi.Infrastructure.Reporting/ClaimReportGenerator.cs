using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos.Claim.Reports;

namespace Tsumugi.Infrastructure.Reporting;

/// <summary>
/// 請求関連 PDF 帳票の生成実装。
/// ADR 0013 により QuestPDF Community ライセンスで動作する前提。
/// テストは抽出テキストの内容一致 (CJK) とバイト列一致 (決定論) の両方で検証する。
/// </summary>
public sealed class ClaimReportGenerator(TimeProvider timeProvider) : IClaimReportGenerator
{
    public byte[] GenerateServiceProvisionRecord(ServiceProvisionRecordDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var generatedAt = timeProvider.GetUtcNow();

        var doc = Document.Create(c =>
        {
            c.Page(p =>
            {
                p.Size(PageSizes.A4);
                p.Margin(2, Unit.Centimetre);
                p.DefaultTextStyle(x => x.FontFamily(QuestPdfLicenseConfigurator.NotoSansJpFamilyName).FontSize(10));

                p.Header().Column(col =>
                {
                    col.Spacing(4);
                    col.Item().Text("サービス提供実績記録票").FontSize(16).Bold();
                    col.Item().Text($"対象月: {dto.YearMonth.Year}年{dto.YearMonth.Month}月");
                    col.Item().Text($"事業所: {dto.Office.OfficeName}（{dto.Office.OfficeNumber}）");
                    col.Item().Text($"利用者: {dto.Recipient.KanjiName}（{dto.Certificate.CertificateNumber}）");
                    if (dto.IntensiveSupport is { } intensive)
                    {
                        col.Item().Text(
                            $"集中的支援エピソード開始日: {intensive.StartDate:yyyy/MM/dd}");
                    }
                });

                p.Content().Table(t =>
                {
                    t.ColumnsDefinition(cd =>
                    {
                        for (var i = 0; i < 15; i++) cd.RelativeColumn();
                    });

                    t.Header(h =>
                    {
                        foreach (var header in ColumnHeaders)
                            h.Cell().Text(header).Bold().FontSize(8);
                    });

                    foreach (var day in dto.Days)
                    {
                        AddCell(t, day.ServiceDate.ToString("M/d", CultureInfo.InvariantCulture));
                        AddCell(t, AttendanceLabel(day.Attendance));
                        AddCell(t, day.MealProvided ? "○" : string.Empty);
                        AddCell(t, TransportLabel(day.Transport));
                        AddCell(t, day.ServiceStartTime is { } start ? start.ToString("HH:mm", CultureInfo.InvariantCulture) : string.Empty);
                        AddCell(t, day.ServiceEndTime is { } end ? end.ToString("HH:mm", CultureInfo.InvariantCulture) : string.Empty);
                        AddCell(t, day.AbsenceResponseNote ?? string.Empty);
                        AddCell(t, day.SpecialVisitSupportMinutes is { } m ? m.ToString(CultureInfo.InvariantCulture) : string.Empty);
                        AddCell(t, day.OffsiteSupportApplied ? "○" : string.Empty);
                        AddCell(t, day.MedicalCoordinationType ?? string.Empty);
                        AddCell(t, day.TrialUseSupportType ?? string.Empty);
                        AddCell(t, day.RegionalCollaborationApplied ? "○" : string.Empty);
                        AddCell(t, day.IntensiveSupportApplied ? "○" : string.Empty);
                        AddCell(t, day.EmergencyAdmissionApplied ? "○" : string.Empty);
                        AddCell(t, day.RecipientConfirmation ? "○" : string.Empty);
                    }
                });

                p.Footer().Column(col =>
                {
                    col.Spacing(2);
                    col.Item().AlignCenter().Text(
                        $"claim-master: {dto.SpecVersion.ClaimMasterVersion}　" +
                        $"CSV仕様: {dto.SpecVersion.CsvSpecificationVersion}　" +
                        $"帳票仕様: {dto.SpecVersion.ReportSpecificationVersion}");
                    col.Item().AlignCenter().Text(
                        $"出力日時: {generatedAt.UtcDateTime.ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture)} UTC");
                });
            });
        }).WithMetadata(new DocumentMetadata
        {
            CreationDate = generatedAt,
            ModifiedDate = generatedAt,
        });
        return doc.GeneratePdf();
    }

    public byte[] GenerateClaimInvoice(ClaimInvoiceDto dto) =>
        throw new NotImplementedException("Task 11");

    public byte[] GenerateClaimStatement(ClaimStatementDto dto) =>
        throw new NotImplementedException("Task 12");

    private static readonly string[] ColumnHeaders =
    [
        "日付", "出欠", "食事", "送迎", "開始時刻", "終了時刻", "欠席時対応",
        "特別訪問", "敷地外", "医療連携", "体験利用", "地域協働", "集中的支援", "緊急受入", "利用者確認",
    ];

    private static void AddCell(TableDescriptor t, string text) =>
        t.Cell().Text(text).FontSize(8);

    private static string AttendanceLabel(Tsumugi.Domain.Enums.Attendance attendance) => attendance switch
    {
        Tsumugi.Domain.Enums.Attendance.Present => "出",
        Tsumugi.Domain.Enums.Attendance.Absent => "欠",
        Tsumugi.Domain.Enums.Attendance.AbsenceSupport => "欠支",
        Tsumugi.Domain.Enums.Attendance.Discontinued => "中止",
        _ => string.Empty,
    };

    private static string TransportLabel(Tsumugi.Domain.Enums.TransportKind transport) => transport switch
    {
        Tsumugi.Domain.Enums.TransportKind.None => string.Empty,
        Tsumugi.Domain.Enums.TransportKind.Outbound => "行",
        Tsumugi.Domain.Enums.TransportKind.Inbound => "帰",
        Tsumugi.Domain.Enums.TransportKind.Round => "往復",
        _ => string.Empty,
    };
}
