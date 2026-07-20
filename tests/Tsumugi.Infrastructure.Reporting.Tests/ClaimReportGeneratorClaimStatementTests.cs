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

public sealed class ClaimReportGeneratorClaimStatementTests
{
    [Fact]
    public void GenerateClaimStatement_lists_recipient_with_lines_and_all_detail_fields()
    {
        QuestPdfLicenseConfigurator.Initialize();
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-06-29T00:00:00Z", CultureInfo.InvariantCulture));
        var dto = SingleRecipientDto();

        var text = ExtractText(new ClaimReportGenerator(clock).GenerateClaimStatement(dto));

        text.Should().Contain("介護給付費・訓練等給付費等請求明細書");
        text.Should().Contain("2026年5月");
        text.Should().Contain("テスト事業所").And.Contain("0123456789");
        text.Should().Contain("山田太郎");
        text.Should().Contain("131016", because: "certificate.municipalityNumber (report:benefit-claim-detail:header:001)");
        text.Should().Contain("999999", because: "certificate.subsidyMunicipalityNumber (report:benefit-claim-detail:header:003)");
        text.Should().Contain("1234567890").And.Contain("管理事業所A",
            because: "certificate.upperLimitManagementProviderNumber/Name (report:benefit-claim-detail:upper-limit-management:001)");
        text.Should().Contain("Compliant", because: "claimInput.upperLimitManagementResult (report:benefit-claim-detail:upper-limit-management:003)");
        text.Should().Contain("705", because: "claimInput.upperLimitManagedAmountYen (report:benefit-claim-detail:upper-limit-management:004)");
        text.Should().Contain("500", because: "claimInput.municipalSubsidyAmountYen (report:benefit-claim-detail:summary:015)");
        text.Should().Contain("B_BASE_W1_C20_S1").And.Contain("MEAL_PROVISION_I", because: "claim-lines 内訳のserviceCode");
        text.Should().Contain("6,720").And.Contain("336", because: "claim-lines のamountYen");
        text.Should().Contain("630", because: "小計 単位数 = SubtotalUnit");
        text.Should().Contain("7,056", because: "小計 費用額 = SubtotalCostYen");
        text.Should().Contain("6,351", because: "小計 給付費請求額 = SubtotalBenefitYen");
    }

    [Fact]
    public void GenerateClaimStatement_is_deterministic_for_same_inputs_and_timeprovider()
    {
        QuestPdfLicenseConfigurator.Initialize();
        var clock1 = new FakeTimeProvider(DateTimeOffset.Parse("2026-06-29T00:00:00Z", CultureInfo.InvariantCulture));
        var clock2 = new FakeTimeProvider(DateTimeOffset.Parse("2026-06-29T00:00:00Z", CultureInfo.InvariantCulture));
        var dto = SingleRecipientDto();

        var a = new ClaimReportGenerator(clock1).GenerateClaimStatement(dto);
        var b = new ClaimReportGenerator(clock2).GenerateClaimStatement(dto);

        a.Should().Equal(b);
    }

    [Fact]
    public void GenerateClaimStatement_totals_equal_sum_of_recipient_subtotals()
    {
        QuestPdfLicenseConfigurator.Initialize();
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-06-29T00:00:00Z", CultureInfo.InvariantCulture));
        var dto = TwoRecipientDto();

        // フィクスチャ自体が「明細合計 = header 合計」を満たしていることを先に固定する。
        dto.TotalUnit.Should().Be(dto.Recipients.Sum(r => r.SubtotalUnit));
        dto.TotalCostYen.Should().Be(dto.Recipients.Sum(r => r.SubtotalCostYen));
        dto.TotalBenefitYen.Should().Be(dto.Recipients.Sum(r => r.SubtotalBenefitYen));
        dto.TotalBurdenYen.Should().Be(dto.Recipients.Sum(r => r.SubtotalBurdenYen));

        var text = ExtractText(new ClaimReportGenerator(clock).GenerateClaimStatement(dto));

        text.Should().Contain("1,130", because: "事業所合計 単位数 = TotalUnit（630+500）");
        text.Should().Contain("12,656", because: "事業所合計 費用額 = TotalCostYen（7,056+5,600）");
        text.Should().Contain("11,391", because: "事業所合計 給付費請求額 = TotalBenefitYen（6,351+5,040）");
        text.Should().Contain("1,265", because: "事業所合計 利用者負担合計 = TotalBurdenYen（705+560）");
    }

    [Fact]
    public void GenerateClaimStatement_lists_multiple_recipients()
    {
        QuestPdfLicenseConfigurator.Initialize();
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-06-29T00:00:00Z", CultureInfo.InvariantCulture));
        var dto = TwoRecipientDto();

        var text = ExtractText(new ClaimReportGenerator(clock).GenerateClaimStatement(dto));

        text.Should().Contain("山田太郎").And.Contain("9876543210");
        text.Should().Contain("鈴木花子").And.Contain("1111111111");
        text.Should().Contain("B_BASE_W1_C20_S1").And.Contain("B_BASE_W1_C20_S2");
    }

    [Fact]
    public void GenerateClaimStatement_omits_claim_input_summary_and_optional_certificate_lines_when_absent()
    {
        QuestPdfLicenseConfigurator.Initialize();
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-06-29T00:00:00Z", CultureInfo.InvariantCulture));
        var dto = SingleRecipientDto() with
        {
            Recipients =
            [
                SingleRecipientDto().Recipients[0] with
                {
                    Certificate = new ClaimReportCertificateDto("9876543210", "131016", null, 9300, null, null),
                    ClaimInput = new ClaimInputSummaryDto(null, null, null),
                },
            ],
        };

        var text = ExtractText(new ClaimReportGenerator(clock).GenerateClaimStatement(dto));

        text.Should().NotContain("自治体助成市町村番号", because: "SubsidyMunicipalityNumber が null のときは行自体を出力しない");
        text.Should().NotContain("上限管理事業所", because: "UpperLimitManagementProviderNumber が null のときは行自体を出力しない");
        text.Should().NotContain("上限管理結果", because: "ClaimInput の3フィールドが全て null のときは summary 行を出力しない");
        text.Should().NotContain("上限管理後利用者負担額");
        text.Should().NotContain("自治体助成分請求額");
    }

    [Fact]
    public void GenerateClaimStatement_throws_on_null_dto()
    {
        var gen = new ClaimReportGenerator(TimeProvider.System);
        FluentActions.Invoking(() => gen.GenerateClaimStatement(null!)).Should().Throw<ArgumentNullException>();
    }

    private static ClaimStatementDto SingleRecipientDto() =>
        new(
            Office: SampleOffice(),
            YearMonth: new YearMonth(2026, 5),
            Recipients:
            [
                new RecipientClaimDetailDto(
                    Recipient: new ClaimReportRecipientDto("山田太郎", "ヤマダタロウ"),
                    Certificate: new ClaimReportCertificateDto(
                        "9876543210", "131016", "999999", 9300, "1234567890", "管理事業所A"),
                    Lines:
                    [
                        new ClaimLineDto(ClaimDetailLineKind.Basic, "B_BASE_W1_C20_S1", 600, 20, 6720),
                        new ClaimLineDto(ClaimDetailLineKind.Addition, "MEAL_PROVISION_I", 30, 20, 336),
                    ],
                    SubtotalUnit: 630, SubtotalCostYen: 7056, SubtotalBenefitYen: 6351, SubtotalBurdenYen: 705,
                    ClaimInput: new ClaimInputSummaryDto("Compliant", 705, 500)),
            ],
            TotalUnit: 630, TotalCostYen: 7056, TotalBenefitYen: 6351, TotalBurdenYen: 705,
            SpecVersion: SampleSpecVersion());

    private static ClaimStatementDto TwoRecipientDto()
    {
        var second = new RecipientClaimDetailDto(
            Recipient: new ClaimReportRecipientDto("鈴木花子", "スズキハナコ"),
            Certificate: new ClaimReportCertificateDto("1111111111", "131017", null, 37200, null, null),
            Lines: [new ClaimLineDto(ClaimDetailLineKind.Basic, "B_BASE_W1_C20_S2", 500, 15, 5600)],
            SubtotalUnit: 500, SubtotalCostYen: 5600, SubtotalBenefitYen: 5040, SubtotalBurdenYen: 560,
            ClaimInput: new ClaimInputSummaryDto(null, null, null));

        var first = SingleRecipientDto();
        return first with
        {
            Recipients = [first.Recipients[0], second],
            TotalUnit = first.TotalUnit + second.SubtotalUnit,
            TotalCostYen = first.TotalCostYen + second.SubtotalCostYen,
            TotalBenefitYen = first.TotalBenefitYen + second.SubtotalBenefitYen,
            TotalBurdenYen = first.TotalBurdenYen + second.SubtotalBurdenYen,
        };
    }

    private static ClaimReportOfficeDto SampleOffice() =>
        new("0123456789", "テスト事業所", RegionGrade.None,
            "1000001", "東京都千代田区千代田1-1", "03-0000-0000", "代表取締役 山田太郎");

    private static ClaimReportSpecVersionDto SampleSpecVersion() =>
        new("r6-2026-04", "r7-10", "r1-10");

    private static string ExtractText(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var pdf = PdfDocument.Open(stream);
        var sb = new StringBuilder();
        foreach (var page in pdf.GetPages()) sb.Append(page.Text);
        return KangxiRadicalNormalizer.FoldKangxiRadicals(sb.ToString());
    }
}
