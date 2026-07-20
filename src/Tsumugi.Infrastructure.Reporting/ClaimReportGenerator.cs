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
                            $"集中的支援エピソード開始日: {intensive.StartDate.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture)}");
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

    public byte[] GenerateClaimInvoice(ClaimInvoiceDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var generatedAt = timeProvider.GetUtcNow();

        var doc = Document.Create(c =>
        {
            c.Page(p =>
            {
                p.Size(PageSizes.A4);
                p.Margin(2, Unit.Centimetre);
                p.DefaultTextStyle(x => x.FontFamily(QuestPdfLicenseConfigurator.NotoSansJpFamilyName).FontSize(11));

                p.Header().Column(col =>
                {
                    col.Spacing(4);
                    col.Item().AlignCenter().Text("介護給付費・訓練等給付費等請求書").FontSize(18).Bold();
                    col.Item().Text($"請求対象月: {dto.YearMonth.Year}年{dto.YearMonth.Month}月");
                    col.Item().Text($"事業所: {dto.Office.OfficeName}（{dto.Office.OfficeNumber}）");
                    col.Item().Text($"郵便番号: {dto.Office.PostalCode}");
                    col.Item().Text($"所在地: {dto.Office.Address}");
                    col.Item().Text($"電話番号: {dto.Office.PhoneNumber}");
                    col.Item().Text($"代表者: {dto.Office.RepresentativeTitleAndName}");
                });

                p.Content().Table(t =>
                {
                    t.ColumnsDefinition(cd =>
                    {
                        cd.RelativeColumn(2);
                        cd.RelativeColumn(1);
                    });

                    AddTotalRow(t, "総単位数", dto.TotalUnit);
                    AddTotalRow(t, "総費用額（円）", dto.TotalCostYen);
                    AddTotalRow(t, "給付費請求額（円）", dto.TotalBenefitYen);
                    AddTotalRow(t, "利用者負担合計（円）", dto.TotalBurdenYen);
                });

                p.Footer().Column(col =>
                {
                    col.Spacing(2);
                    AddVersionLine(col, "claim-master", dto.SpecVersion.ClaimMasterVersion);
                    AddVersionLine(col, "CSV仕様", dto.SpecVersion.CsvSpecificationVersion);
                    AddVersionLine(col, "帳票仕様", dto.SpecVersion.ReportSpecificationVersion);
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

    public byte[] GenerateClaimStatement(ClaimStatementDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var generatedAt = timeProvider.GetUtcNow();

        var doc = Document.Create(c =>
        {
            c.Page(p =>
            {
                p.Size(PageSizes.A4);
                p.Margin(1.5f, Unit.Centimetre);
                p.DefaultTextStyle(x => x.FontFamily(QuestPdfLicenseConfigurator.NotoSansJpFamilyName).FontSize(9));

                p.Header().Column(col =>
                {
                    col.Spacing(4);
                    col.Item().AlignCenter().Text("介護給付費・訓練等給付費等請求明細書").FontSize(14).Bold();
                    col.Item().Text($"対象月: {dto.YearMonth.Year}年{dto.YearMonth.Month}月");
                    col.Item().Text($"事業所: {dto.Office.OfficeName}（{dto.Office.OfficeNumber}）");
                });

                p.Content().Column(col =>
                {
                    col.Spacing(10);
                    for (var i = 0; i < dto.Recipients.Count; i++)
                    {
                        if (i > 0) col.Item().LineHorizontal(1, Unit.Point);
                        AddRecipientSection(col.Item(), dto.Recipients[i]);
                    }
                });

                p.Footer().Column(col =>
                {
                    col.Spacing(2);
                    col.Item().Text(
                        $"事業所合計 単位数: {dto.TotalUnit.ToString("N0", CultureInfo.InvariantCulture)} / " +
                        $"費用額: {dto.TotalCostYen.ToString("N0", CultureInfo.InvariantCulture)}円 / " +
                        $"給付費請求額: {dto.TotalBenefitYen.ToString("N0", CultureInfo.InvariantCulture)}円 / " +
                        $"利用者負担合計: {dto.TotalBurdenYen.ToString("N0", CultureInfo.InvariantCulture)}円");
                    AddVersionLine(col, "claim-master", dto.SpecVersion.ClaimMasterVersion);
                    AddVersionLine(col, "CSV仕様", dto.SpecVersion.CsvSpecificationVersion);
                    AddVersionLine(col, "帳票仕様", dto.SpecVersion.ReportSpecificationVersion);
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

    private static readonly string[] ColumnHeaders =
    [
        "日付", "出欠", "食事", "送迎", "開始時刻", "終了時刻", "欠席時対応",
        "特別訪問", "敷地外", "医療連携", "体験利用", "地域協働", "集中的支援", "緊急受入", "利用者確認",
    ];

    private static void AddCell(TableDescriptor t, string text) =>
        t.Cell().Text(text).FontSize(8);

    private static void AddTotalRow(TableDescriptor t, string label, int amountYen)
    {
        t.Cell().Text(label);
        t.Cell().AlignRight().Text(amountYen.ToString("N0", CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// 請求明細書の受給者1名分のセクション（ヘッダ + 明細行テーブル + 小計 + 請求入力summary）を描画する。
    /// </summary>
    private static void AddRecipientSection(IContainer container, RecipientClaimDetailDto recipient)
    {
        container.Column(col =>
        {
            col.Spacing(3);
            var certificate = recipient.Certificate;

            col.Item().Text($"{recipient.Recipient.KanjiName}（{certificate.CertificateNumber}）").Bold();
            col.Item().Text($"市町村番号: {certificate.MunicipalityNumber}");
            if (certificate.SubsidyMunicipalityNumber is { } subsidyMunicipalityNumber)
                col.Item().Text($"自治体助成市町村番号: {subsidyMunicipalityNumber}");
            if (certificate.UpperLimitManagementProviderNumber is { } providerNumber)
            {
                col.Item().Text(certificate.UpperLimitManagementProviderName is { } providerName
                    ? $"上限管理事業所: {providerNumber}　{providerName}"
                    : $"上限管理事業所: {providerNumber}");
            }

            col.Item().Table(t =>
            {
                t.ColumnsDefinition(cd =>
                {
                    cd.RelativeColumn(2);
                    cd.RelativeColumn(3);
                    cd.RelativeColumn(2);
                    cd.RelativeColumn(2);
                });
                t.Header(h =>
                {
                    h.Cell().Text("種別").Bold();
                    h.Cell().Text("コード").Bold();
                    h.Cell().AlignRight().Text("単位数").Bold();
                    h.Cell().AlignRight().Text("金額(円)").Bold();
                });
                foreach (var line in recipient.Lines)
                {
                    t.Cell().Text(line.Kind.ToString());
                    AddSplitAsciiText(t.Cell(), line.ServiceCode);
                    t.Cell().AlignRight().Text(line.Unit.ToString("N0", CultureInfo.InvariantCulture));
                    t.Cell().AlignRight().Text(line.AmountYen.ToString("N0", CultureInfo.InvariantCulture));
                }
            });

            col.Item().Text(
                $"小計 単位数: {recipient.SubtotalUnit.ToString("N0", CultureInfo.InvariantCulture)} / " +
                $"費用額: {recipient.SubtotalCostYen.ToString("N0", CultureInfo.InvariantCulture)}円 / " +
                $"給付費請求額: {recipient.SubtotalBenefitYen.ToString("N0", CultureInfo.InvariantCulture)}円 / " +
                $"利用者負担: {recipient.SubtotalBurdenYen.ToString("N0", CultureInfo.InvariantCulture)}円");

            var claimInput = recipient.ClaimInput;
            if (claimInput.UpperLimitManagementResult is not null
                || claimInput.UpperLimitManagedAmountYen is not null
                || claimInput.MunicipalSubsidyAmountYen is not null)
            {
                if (claimInput.UpperLimitManagementResult is { } result)
                    col.Item().Text($"上限管理結果: {result}");
                if (claimInput.UpperLimitManagedAmountYen is { } managedAmountYen)
                    col.Item().Text($"上限管理後利用者負担額: {managedAmountYen.ToString("N0", CultureInfo.InvariantCulture)}円");
                if (claimInput.MunicipalSubsidyAmountYen is { } subsidyAmountYen)
                    col.Item().Text($"自治体助成分請求額: {subsidyAmountYen.ToString("N0", CultureInfo.InvariantCulture)}円");
            }
        });
    }

    /// <summary>
    /// フッタの仕様バージョン識別子（例: "r6-2026-04"）を1行描画する。
    /// 英字接頭辞に数字が直接続くASCII文字列を、CJKラベルと同一の QuestPDF Text() 呼び出しで
    /// 描画すると、Skia の文字整形結果に対して PdfPig が ToUnicode を誤って解決し、
    /// 抽出テキストでは接頭辞直後の数字以降が NUL (U+0000) に化ける事象を Task 11 で確認した
    /// （生成される PDF の見た目のグリフ自体は正しく、抽出テキストのみに影響）。
    /// 英字接頭辞を含むラベル部分と、先頭が数字になる残り部分を Row 上の別 Text 要素に分離す
    /// ることで、Skia が両者を別々の文字整形単位として扱い、抽出結果の破損を避けられることを
    /// 実測で確認済み。Row の AutoItem は既定で隙間を空けないため、視覚的には連続した1行として
    /// 表示される。
    /// </summary>
    private static void AddVersionLine(ColumnDescriptor col, string label, string version)
    {
        var digitStart = 0;
        while (digitStart < version.Length && !char.IsAsciiDigit(version[digitStart])) digitStart++;

        col.Item().Row(r =>
        {
            r.AutoItem().Text($"{label}: {version[..digitStart]}");
            if (digitStart < version.Length)
                r.AutoItem().Text(version[digitStart..]);
        });
    }

    /// <summary>
    /// サービスコード等、AddVersionLine より letter/digit 混在パターンが複雑な任意の ASCII 文字列を
    /// PdfPig 抽出破損なしで描画する（Task 12 で実験的に特定した一般化）。
    /// AddVersionLine のコメントは「英字直後に数字が続く境界」が原因と記していたが、実機検証の結果、
    /// 実際の破損条件はより広く「1つの QuestPDF Text() 呼び出し文字列内に ASCII 英字と ASCII 数字が
    /// 両方存在する」ことそのものであり、両者が隣接しているかは無関係（例: "r6-2026-04" を1つの
    /// Text() で描画すると、'r' から離れた "2026"/"04" の数字も含め全ての数字が破損する）。
    /// 見た目のグリフ自体は正しく、抽出テキストのみに影響する点は Task 11 の所見と同じ。
    /// 対策として、文字列を「数字だけの連続」と「数字を含まない連続」に交互分割し、各断片を Row 上の
    /// 別 Text 要素として描画する（断片単位では英字と数字が共存しないため破損しない）。
    /// 例: "B_BASE_W1_C20_S1" → ["B_BASE_W", "1", "_C", "20", "_S", "1"]。
    /// </summary>
    private static void AddSplitAsciiText(IContainer container, string text)
    {
        var segments = new List<string>();
        var start = 0;
        for (var i = 1; i < text.Length; i++)
        {
            if (char.IsAsciiDigit(text[i]) != char.IsAsciiDigit(text[i - 1]))
            {
                segments.Add(text[start..i]);
                start = i;
            }
        }
        segments.Add(text[start..]);

        container.Row(r =>
        {
            foreach (var segment in segments)
                r.AutoItem().Text(segment);
        });
    }

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
