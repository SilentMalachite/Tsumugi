using Tsumugi.Application.Dtos.Claim.Csv;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Infrastructure.Csv.Tests.Generation;

/// <summary>決定論的な CSV 生成入力（固定日付・固定コード）。テスト間で値を共有する。</summary>
internal static class ClaimCsvFixtures
{
    internal static ClaimCsvDto Normal() => new(
        ProcessingMonth: new ProcessingMonth(2026, 8),
        ServiceMonth: new ServiceMonth(2026, 7),
        Office: new ClaimCsvOfficeDto(
            OfficeNumber: "1312345678",
            RegionGrade: Tsumugi.Domain.Enums.RegionGrade.Grade6,
            UnitPriceMilliYen: 10_000),
        Recipients: [Recipient("1234567890")],
        Totals: new ClaimCsvTotalsDto(
            TotalUnits: 3_000,
            TotalCostYen: 30_000,
            TotalBenefitYen: 27_000,
            TotalBurdenYen: 3_000),
        SpecVersion: new ClaimCsvSpecVersionDto("r7-10", "master-v1"));

    /// <param name="specialVisitSupportBilledCount">
    /// 訪問支援特別加算の算定回数（provider:J611:01:052）。サービス提供回数（同 :051）とは
    /// 別概念なので、fixture でも意図的に別の値を持たせる。
    /// </param>
    /// <param name="offsiteSupportCumulativeDays">
    /// 施設外支援の累計日数（provider:J611:01:054）。当月日数（同 :053）とは別の値を持たせる。
    /// </param>
    internal static ClaimCsvRecipientDto Recipient(
        string certificateNumber,
        string kanaName = "ﾂﾑｷﾞ ﾀﾛｳ",
        int? upperLimitManagementResultCode = null,
        int? upperLimitManagedAmountYen = null,
        int? specialVisitSupportBilledCount = 2,
        int? offsiteSupportCumulativeDays = 12) => new(
        SortKey: certificateNumber,
        CertificateNumber: certificateNumber,
        MunicipalityNumber: "131016",
        SubsidyMunicipalityNumber: null,
        RecipientKanaName: kanaName,
        MonthlyCostCapYen: 9_300,
        UpperLimitManagementProviderNumber: null,
        UpperLimitManagementResultCode: upperLimitManagementResultCode,
        UpperLimitManagedAmountYen: upperLimitManagedAmountYen,
        MunicipalSubsidyAmountYen: null,
        ExceptionalUsageStartMonth: null,
        ExceptionalUsageEndMonth: null,
        ExceptionalUsageDays: null,
        StandardUsageDayTotal: null,
        IntensiveSupportEpisodeStartDate: null,
        Contract: new ClaimCsvContractDto(
            ContractedSupplyDays: 22,
            ContractDate: new DateOnly(2026, 4, 1),
            TerminationDate: null,
            CertificateEntryNumber: 1,
            FirstServiceDate: new DateOnly(2026, 4, 1)),
        ServiceLines:
        [
            new ClaimCsvServiceLineDto("462980", Unit: 566, Count: 5),
            new ClaimCsvServiceLineDto("466010", Unit: 34, Count: 5),
        ],
        DailyRecords:
        [
            Day(new DateOnly(2026, 7, 1)),
            Day(new DateOnly(2026, 7, 2), mealProvided: true),
            Day(new DateOnly(2026, 7, 3), transportCode: 3),
            Day(new DateOnly(2026, 7, 6), offsiteSupportApplied: true),
            // 訪問支援特別加算。サービス提供時間は分で持ち（90 分 = 1.5 時間 → 1/100 時間で 150）、
            // 算定時間数は計画に基づく別項目なので別の値（2 時間）を持たせる。
            Day(new DateOnly(2026, 7, 7), specialVisitSupportMinutes: 90, specialVisitSupportBilledHours: 2),
        ],
        BilledDays: 5,
        // 欠席時対応加算のみの日を 1 日含む（サービス利用日数 = 本体5日 + 加算のみ1日）。
        ServiceUsageDays: 6,
        TotalUnits: 3_000,
        TotalCostYen: 30_000,
        BenefitYen: 27_000,
        BurdenYen: 3_000,
        SpecialVisitSupportBilledCount: specialVisitSupportBilledCount,
        OffsiteSupportCumulativeDays: offsiteSupportCumulativeDays);

    /// <param name="specialVisitSupportMinutes">
    /// 実際にサービス提供した時間（分）。CSV へは 1/100 時間で出るため、3 の倍数を使う
    /// （3 の倍数でない分は公式の丸め規則が定まっておらず fail-close する）。
    /// </param>
    /// <param name="specialVisitSupportBilledHours">
    /// 算定時間数（時間・整数）。<paramref name="specialVisitSupportMinutes"/> からは導出できない別項目。
    /// </param>
    internal static ClaimCsvDailyRecordDto Day(
        DateOnly serviceDate,
        int attendanceCode = 1,
        bool mealProvided = false,
        int transportCode = 0,
        int? specialVisitSupportMinutes = null,
        int? specialVisitSupportBilledHours = null,
        bool offsiteSupportApplied = false) => new(
        ServiceDate: serviceDate,
        AttendanceCode: attendanceCode,
        MealProvided: mealProvided,
        TransportCode: transportCode,
        ServiceStartTime: new TimeOnly(9, 30),
        ServiceEndTime: new TimeOnly(15, 30),
        SpecialVisitSupportMinutes: specialVisitSupportMinutes,
        OffsiteSupportApplied: offsiteSupportApplied,
        MedicalCoordinationCode: null,
        TrialUseSupportCode: null,
        RegionalCollaborationApplied: false,
        IntensiveSupportApplied: false,
        EmergencyAdmissionApplied: false,
        SpecialVisitSupportBilledHours: specialVisitSupportBilledHours);
}
