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

public sealed class ClaimReportGeneratorClaimInvoiceTests
{
    [Fact]
    public void GenerateClaimInvoice_shows_office_contact_totals_and_spec_versions()
    {
        QuestPdfLicenseConfigurator.Initialize();
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-06-29T00:00:00Z", CultureInfo.InvariantCulture));
        var gen = new ClaimReportGenerator(clock);
        var dto = SampleDto();

        var text = ExtractText(gen.GenerateClaimInvoice(dto));

        text.Should().Contain("介護給付費・訓練等給付費等請求書");
        text.Should().Contain("2026年5月");
        text.Should().Contain("テスト事業所");
        text.Should().Contain("1000001", because: "郵便番号 (report:benefit-claim-form:header:004)")
            .And.Contain("東京都千代田区千代田1-1", because: "所在地 (:005)")
            .And.Contain("03-0000-0000", because: "電話番号 (:006)")
            .And.Contain("代表取締役 山田太郎", because: "代表者職氏名 (:008)");
        text.Should().Contain("12,000", because: "総単位数は3桁区切り InvariantCulture で出力される");
        text.Should().Contain("1,344,000", because: "総費用額（円）は3桁区切り InvariantCulture で出力される");
        text.Should().Contain("1,209,600", because: "給付費請求額（円） = TotalBenefitYen");
        text.Should().Contain("134,400", because: "利用者負担合計（円） = TotalBurdenYen");
        text.Should().Contain("r6-2026-04", because: "claim-master バージョンがフッタに出力される");
        text.Should().Contain("r7-10");
        text.Should().Contain("r1-10");
    }

    [Fact]
    public void GenerateClaimInvoice_is_deterministic_for_same_inputs_and_timeprovider()
    {
        QuestPdfLicenseConfigurator.Initialize();
        var clock1 = new FakeTimeProvider(DateTimeOffset.Parse("2026-06-29T00:00:00Z", CultureInfo.InvariantCulture));
        var clock2 = new FakeTimeProvider(DateTimeOffset.Parse("2026-06-29T00:00:00Z", CultureInfo.InvariantCulture));
        var dto = SampleDto();

        var a = new ClaimReportGenerator(clock1).GenerateClaimInvoice(dto);
        var b = new ClaimReportGenerator(clock2).GenerateClaimInvoice(dto);

        a.Should().Equal(b);
    }

    [Fact]
    public void GenerateClaimInvoice_throws_on_null_dto()
    {
        var gen = new ClaimReportGenerator(TimeProvider.System);
        FluentActions.Invoking(() => gen.GenerateClaimInvoice(null!)).Should().Throw<ArgumentNullException>();
    }

    private static ClaimInvoiceDto SampleDto() =>
        new(
            Office: new ClaimReportOfficeDto("0123456789", "テスト事業所", RegionGrade.None,
                "1000001", "東京都千代田区千代田1-1", "03-0000-0000", "代表取締役 山田太郎"),
            YearMonth: new YearMonth(2026, 5),
            TotalUnit: 12000,
            TotalCostYen: 1_344_000,
            TotalBenefitYen: 1_209_600,
            TotalBurdenYen: 134_400,
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
