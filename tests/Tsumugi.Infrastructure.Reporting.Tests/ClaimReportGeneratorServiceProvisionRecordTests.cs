using System.Globalization;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Tsumugi.Application.Dtos.Claim.Reports;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;
using UglyToad.PdfPig;
using Xunit;

namespace Tsumugi.Infrastructure.Reporting.Tests;

public sealed class ClaimReportGeneratorServiceProvisionRecordTests
{
    [Fact]
    public void GenerateServiceProvisionRecord_includes_office_recipient_month_in_extracted_text()
    {
        QuestPdfLicenseConfigurator.Initialize();
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-06-29T00:00:00Z", CultureInfo.InvariantCulture));
        var gen = new ClaimReportGenerator(clock);
        var dto = SampleDto();
        var bytes = gen.GenerateServiceProvisionRecord(dto);
        var text = ExtractText(bytes);
        text.Should().Contain("テスト事業所").And.Contain("山田太郎").And.Contain("2026年5月");
        text.Should().Contain("サービス提供実績記録票");
    }

    [Fact]
    public void GenerateServiceProvisionRecord_renders_all_10_daily_fields()
    {
        QuestPdfLicenseConfigurator.Initialize();
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-06-29T00:00:00Z", CultureInfo.InvariantCulture));
        var dto = SampleDto();
        var text = ExtractText(new ClaimReportGenerator(clock).GenerateServiceProvisionRecord(dto));
        text.Should().Contain("09:00").And.Contain("16:00");                    // ServiceStartTime/EndTime
        text.Should().Contain("特別訪問");                                          // SpecialVisitSupportMinutes ヘッダ
        text.Should().Contain("敷地外");                                            // OffsiteSupportApplied
        text.Should().Contain("医療連携");                                          // MedicalCoordinationType
        text.Should().Contain("体験利用");                                          // TrialUseSupportType
        text.Should().Contain("地域協働");                                          // RegionalCollaborationApplied
        text.Should().Contain("集中的支援");                                        // IntensiveSupportApplied
        text.Should().Contain("緊急受入");                                          // EmergencyAdmissionApplied
        text.Should().Contain("利用者確認");                                        // RecipientConfirmation
    }

    [Fact]
    public void GenerateServiceProvisionRecord_is_deterministic_for_same_inputs_and_timeprovider()
    {
        QuestPdfLicenseConfigurator.Initialize();
        var clock1 = new FakeTimeProvider(DateTimeOffset.Parse("2026-06-29T00:00:00Z", CultureInfo.InvariantCulture));
        var clock2 = new FakeTimeProvider(DateTimeOffset.Parse("2026-06-29T00:00:00Z", CultureInfo.InvariantCulture));
        var dto = SampleDto();
        var a = new ClaimReportGenerator(clock1).GenerateServiceProvisionRecord(dto);
        var b = new ClaimReportGenerator(clock2).GenerateServiceProvisionRecord(dto);
        a.Should().Equal(b);
    }

    [Fact]
    public void GenerateServiceProvisionRecord_includes_intensive_support_start_date()
    {
        QuestPdfLicenseConfigurator.Initialize();
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-06-29T00:00:00Z", CultureInfo.InvariantCulture));
        var dto = SampleDto() with { IntensiveSupport = new IntensiveSupportEpisodeDto(new DateOnly(2026, 5, 1)) };
        var text = ExtractText(new ClaimReportGenerator(clock).GenerateServiceProvisionRecord(dto));
        text.Should().Contain("集中的支援エピソード開始日");
    }

    [Fact]
    public void GenerateClaimInvoice_and_GenerateClaimStatement_are_not_yet_implemented()
    {
        var gen = new ClaimReportGenerator(TimeProvider.System);
        FluentActions.Invoking(() => gen.GenerateClaimInvoice(null!)).Should().Throw<NotImplementedException>();
        FluentActions.Invoking(() => gen.GenerateClaimStatement(null!)).Should().Throw<NotImplementedException>();
    }

    private static ServiceProvisionRecordDto SampleDto() =>
        new(
            Office: new ClaimReportOfficeDto("0123456789", "テスト事業所", RegionGrade.None,
                "1000001", "東京都千代田区千代田1-1", "03-0000-0000", "代表取締役 山田太郎"),
            Recipient: new ClaimReportRecipientDto("山田太郎", "ヤマダタロウ"),
            Certificate: new ClaimReportCertificateDto("9876543210", "131016", null, 9300, null, null),
            YearMonth: new YearMonth(2026, 5),
            Days: [
                new DailyServiceRecordDto(
                    new DateOnly(2026, 5, 1), Attendance.Present, true, TransportKind.None, null,
                    new TimeOnly(9, 0), new TimeOnly(16, 0), null, false, null, null,
                    false, false, false, true)
            ],
            IntensiveSupport: null,
            SpecVersion: new ClaimReportSpecVersionDto("r6-2026-04", "r7-10", "r1-10"));

    private static string ExtractText(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var pdf = PdfDocument.Open(stream);
        var sb = new StringBuilder();
        foreach (var page in pdf.GetPages()) sb.Append(page.Text);
        return KangxiRadicalNormalizer.FoldKangxiRadicals(sb.ToString());
    }
}
