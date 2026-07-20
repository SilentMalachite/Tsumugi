using FluentAssertions;
using Tsumugi.Application.Dtos.Claim.Reports;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Application.Tests.Dtos.Claim.Reports;

/// <summary>
/// spec §7 の帳票 DTO 12個について、record 定義通りに全フィールドが構築・保持されることを
/// 確認する smoke test（Task 9 は scaffolding のみでロジックを持たないため、動作検証は本テストで足りる）。
/// </summary>
public sealed class ClaimReportDtosSmokeTests
{
    [Fact]
    public void ClaimReportOfficeDto_holds_all_fields()
    {
        var dto = new ClaimReportOfficeDto(
            OfficeNumber: "1234567890",
            OfficeName: "紡ぎ事業所",
            RegionGrade: RegionGrade.Grade3,
            PostalCode: "100-0001",
            Address: "東京都千代田区1-1-1",
            PhoneNumber: "03-1234-5678",
            RepresentativeTitleAndName: "管理者 山田太郎");

        dto.OfficeNumber.Should().Be("1234567890");
        dto.OfficeName.Should().Be("紡ぎ事業所");
        dto.RegionGrade.Should().Be(RegionGrade.Grade3);
        dto.PostalCode.Should().Be("100-0001");
        dto.Address.Should().Be("東京都千代田区1-1-1");
        dto.PhoneNumber.Should().Be("03-1234-5678");
        dto.RepresentativeTitleAndName.Should().Be("管理者 山田太郎");
    }

    [Fact]
    public void ClaimReportRecipientDto_holds_all_fields()
    {
        var dto = new ClaimReportRecipientDto(KanjiName: "利用者花子", KanaName: "リヨウシャハナコ");

        dto.KanjiName.Should().Be("利用者花子");
        dto.KanaName.Should().Be("リヨウシャハナコ");
    }

    [Fact]
    public void ClaimReportCertificateDto_holds_all_fields()
    {
        var dto = new ClaimReportCertificateDto(
            CertificateNumber: "0000000001",
            MunicipalityNumber: "131016",
            SubsidyMunicipalityNumber: "131017",
            MonthlyCostCap: 9300,
            UpperLimitManagementProviderNumber: "9876543210",
            UpperLimitManagementProviderName: "他事業所");

        dto.CertificateNumber.Should().Be("0000000001");
        dto.MunicipalityNumber.Should().Be("131016");
        dto.SubsidyMunicipalityNumber.Should().Be("131017");
        dto.MonthlyCostCap.Should().Be(9300);
        dto.UpperLimitManagementProviderNumber.Should().Be("9876543210");
        dto.UpperLimitManagementProviderName.Should().Be("他事業所");
    }

    [Fact]
    public void ClaimReportSpecVersionDto_holds_all_fields()
    {
        var dto = new ClaimReportSpecVersionDto(
            ClaimMasterVersion: "2024-04",
            CsvSpecificationVersion: "v2",
            ReportSpecificationVersion: "r1");

        dto.ClaimMasterVersion.Should().Be("2024-04");
        dto.CsvSpecificationVersion.Should().Be("v2");
        dto.ReportSpecificationVersion.Should().Be("r1");
    }

    [Fact]
    public void DailyServiceRecordDto_holds_all_fields()
    {
        var dto = new DailyServiceRecordDto(
            ServiceDate: new DateOnly(2026, 6, 1),
            Attendance: Attendance.Present,
            MealProvided: true,
            Transport: TransportKind.Round,
            AbsenceResponseNote: "備考",
            ServiceStartTime: new TimeOnly(9, 0),
            ServiceEndTime: new TimeOnly(15, 0),
            SpecialVisitSupportMinutes: 30,
            OffsiteSupportApplied: true,
            MedicalCoordinationType: "type-a",
            TrialUseSupportType: "trial-a",
            RegionalCollaborationApplied: true,
            IntensiveSupportApplied: true,
            EmergencyAdmissionApplied: false,
            RecipientConfirmation: true);

        dto.ServiceDate.Should().Be(new DateOnly(2026, 6, 1));
        dto.Attendance.Should().Be(Attendance.Present);
        dto.MealProvided.Should().BeTrue();
        dto.Transport.Should().Be(TransportKind.Round);
        dto.AbsenceResponseNote.Should().Be("備考");
        dto.ServiceStartTime.Should().Be(new TimeOnly(9, 0));
        dto.ServiceEndTime.Should().Be(new TimeOnly(15, 0));
        dto.SpecialVisitSupportMinutes.Should().Be(30);
        dto.OffsiteSupportApplied.Should().BeTrue();
        dto.MedicalCoordinationType.Should().Be("type-a");
        dto.TrialUseSupportType.Should().Be("trial-a");
        dto.RegionalCollaborationApplied.Should().BeTrue();
        dto.IntensiveSupportApplied.Should().BeTrue();
        dto.EmergencyAdmissionApplied.Should().BeFalse();
        dto.RecipientConfirmation.Should().BeTrue();
    }

    [Fact]
    public void IntensiveSupportEpisodeDto_holds_all_fields()
    {
        var dto = new IntensiveSupportEpisodeDto(StartDate: new DateOnly(2026, 6, 15));

        dto.StartDate.Should().Be(new DateOnly(2026, 6, 15));
    }

    [Fact]
    public void ClaimInputSummaryDto_holds_all_fields()
    {
        var dto = new ClaimInputSummaryDto(
            UpperLimitManagementResult: "result-a",
            UpperLimitManagedAmountYen: 1000,
            MunicipalSubsidyAmountYen: 500);

        dto.UpperLimitManagementResult.Should().Be("result-a");
        dto.UpperLimitManagedAmountYen.Should().Be(1000);
        dto.MunicipalSubsidyAmountYen.Should().Be(500);
    }

    [Fact]
    public void ClaimLineDto_holds_all_fields()
    {
        var dto = new ClaimLineDto(
            Kind: ClaimDetailLineKind.Addition,
            ServiceCode: "MEAL_PROVISION_I",
            Unit: 30,
            Count: 20,
            AmountYen: 336);

        dto.Kind.Should().Be(ClaimDetailLineKind.Addition);
        dto.ServiceCode.Should().Be("MEAL_PROVISION_I");
        dto.Unit.Should().Be(30);
        dto.Count.Should().Be(20);
        dto.AmountYen.Should().Be(336);
    }

    [Fact]
    public void ServiceProvisionRecordDto_holds_all_fields()
    {
        var office = SampleOffice();
        var recipient = SampleRecipient();
        var certificate = SampleCertificate();
        var specVersion = SampleSpecVersion();
        var day = SampleDay();
        var intensiveSupport = new IntensiveSupportEpisodeDto(new DateOnly(2026, 6, 15));

        var dto = new ServiceProvisionRecordDto(
            Office: office,
            Recipient: recipient,
            Certificate: certificate,
            YearMonth: new YearMonth(2026, 6),
            Days: [day],
            IntensiveSupport: intensiveSupport,
            SpecVersion: specVersion);

        dto.Office.Should().Be(office);
        dto.Recipient.Should().Be(recipient);
        dto.Certificate.Should().Be(certificate);
        dto.YearMonth.Should().Be(new YearMonth(2026, 6));
        dto.Days.Should().ContainSingle().Which.Should().Be(day);
        dto.IntensiveSupport.Should().Be(intensiveSupport);
        dto.SpecVersion.Should().Be(specVersion);
    }

    [Fact]
    public void ClaimInvoiceDto_holds_all_fields()
    {
        var office = SampleOffice();
        var specVersion = SampleSpecVersion();

        var dto = new ClaimInvoiceDto(
            Office: office,
            YearMonth: new YearMonth(2026, 6),
            TotalUnit: 12000,
            TotalCostYen: 120000,
            TotalBenefitYen: 108000,
            TotalBurdenYen: 12000,
            SpecVersion: specVersion);

        dto.Office.Should().Be(office);
        dto.YearMonth.Should().Be(new YearMonth(2026, 6));
        dto.TotalUnit.Should().Be(12000);
        dto.TotalCostYen.Should().Be(120000);
        dto.TotalBenefitYen.Should().Be(108000);
        dto.TotalBurdenYen.Should().Be(12000);
        dto.SpecVersion.Should().Be(specVersion);
    }

    [Fact]
    public void RecipientClaimDetailDto_holds_all_fields()
    {
        var recipient = SampleRecipient();
        var certificate = SampleCertificate();
        var line = new ClaimLineDto(ClaimDetailLineKind.Basic, "B_BASE_W1_C20_S1", 600, 20, 6720);
        var claimInput = new ClaimInputSummaryDto("result-a", 1000, 500);

        var dto = new RecipientClaimDetailDto(
            Recipient: recipient,
            Certificate: certificate,
            Lines: [line],
            SubtotalUnit: 600,
            SubtotalCostYen: 6720,
            SubtotalBenefitYen: 6000,
            SubtotalBurdenYen: 720,
            ClaimInput: claimInput);

        dto.Recipient.Should().Be(recipient);
        dto.Certificate.Should().Be(certificate);
        dto.Lines.Should().ContainSingle().Which.Should().Be(line);
        dto.SubtotalUnit.Should().Be(600);
        dto.SubtotalCostYen.Should().Be(6720);
        dto.SubtotalBenefitYen.Should().Be(6000);
        dto.SubtotalBurdenYen.Should().Be(720);
        dto.ClaimInput.Should().Be(claimInput);
    }

    [Fact]
    public void ClaimStatementDto_holds_all_fields()
    {
        var office = SampleOffice();
        var specVersion = SampleSpecVersion();
        var recipientDetail = new RecipientClaimDetailDto(
            SampleRecipient(),
            SampleCertificate(),
            [new ClaimLineDto(ClaimDetailLineKind.Basic, "B_BASE_W1_C20_S1", 600, 20, 6720)],
            600,
            6720,
            6000,
            720,
            new ClaimInputSummaryDto(null, null, null));

        var dto = new ClaimStatementDto(
            Office: office,
            YearMonth: new YearMonth(2026, 6),
            Recipients: [recipientDetail],
            TotalUnit: 600,
            TotalCostYen: 6720,
            TotalBenefitYen: 6000,
            TotalBurdenYen: 720,
            SpecVersion: specVersion);

        dto.Office.Should().Be(office);
        dto.YearMonth.Should().Be(new YearMonth(2026, 6));
        dto.Recipients.Should().ContainSingle().Which.Should().Be(recipientDetail);
        dto.TotalUnit.Should().Be(600);
        dto.TotalCostYen.Should().Be(6720);
        dto.TotalBenefitYen.Should().Be(6000);
        dto.TotalBurdenYen.Should().Be(720);
        dto.SpecVersion.Should().Be(specVersion);
    }

    private static ClaimReportOfficeDto SampleOffice() => new(
        "1234567890", "紡ぎ事業所", RegionGrade.Grade3, "100-0001", "東京都千代田区1-1-1",
        "03-1234-5678", "管理者 山田太郎");

    private static ClaimReportRecipientDto SampleRecipient() => new("利用者花子", "リヨウシャハナコ");

    private static ClaimReportCertificateDto SampleCertificate() => new(
        "0000000001", "131016", "131017", 9300, "9876543210", "他事業所");

    private static ClaimReportSpecVersionDto SampleSpecVersion() => new("2024-04", "v2", "r1");

    private static DailyServiceRecordDto SampleDay() => new(
        new DateOnly(2026, 6, 1), Attendance.Present, true, TransportKind.Round, "備考",
        new TimeOnly(9, 0), new TimeOnly(15, 0), 30, true, "type-a", "trial-a", true, true, false, true);
}
