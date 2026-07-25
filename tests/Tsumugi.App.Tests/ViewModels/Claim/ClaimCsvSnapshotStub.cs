using System.Text;
using Tsumugi.Application.Claim;
using Tsumugi.Application.Dtos.Claim.Reports;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.App.Tests.ViewModels.Claim;

/// <summary>ViewModel テスト用の最小 finalization snapshot（canonical JSON）。</summary>
internal static class ClaimCsvSnapshotStub
{
    internal static string Json(ServiceMonth serviceMonth) => Encoding.UTF8.GetString(
        ClaimFinalizationSnapshotWriter.Write(new ClaimFinalizationSnapshot(
            RecipientId: Guid.Parse("33333333-3333-3333-3333-333333333333"),
            ServiceMonth: serviceMonth,
            ClaimMasterVersion: "master-v1",
            CsvSpecificationVersion: "r7-10",
            ReportSpecificationVersion: "report-v1",
            Office: new ClaimFinalizationOfficeSnapshot(
                "1312345678", "つむぎ", RegionGrade.Grade6, "1000001", "住所", "0312345678", "管理者"),
            Recipient: new ClaimFinalizationRecipientSnapshot("紡 太郎", "ﾂﾑｷﾞ ﾀﾛｳ"),
            Certificate: new ClaimFinalizationCertificateSnapshot(
                "1234567890", "131016", null, 9300, null, null),
            ClaimInput: new ClaimFinalizationClaimInputSnapshot(null, null, null, null, null, null, null),
            DailyRecords:
            [
                new ClaimFinalizationDailyRecordSnapshot(
                    new DateOnly(serviceMonth.Year, serviceMonth.Month, 1),
                    Attendance.Present, false, TransportKind.None, null,
                    new TimeOnly(9, 30), new TimeOnly(15, 30), null, false,
                    null, null, false, false, false, true),
            ],
            IntensiveSupportEpisode: null,
            ClaimLines:
            [
                new ClaimFinalizationClaimLineSnapshot(ClaimDetailLineKind.Basic, "462980", 566, 1, 5660),
            ],
            BilledDays: 1,
            TotalUnits: 566,
            TotalCostYen: 5660,
            BenefitYen: 5094,
            BurdenYen: 566)));
}
