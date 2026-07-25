using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Claim;
using Tsumugi.Application.Dtos.Claim.Csv;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.UseCases.Claim;

/// <summary>
/// 国保連請求CSVの出力。<see cref="GenerateClaimReportsUseCase"/> と同じ consumer 側 orchestration で、
/// 確定済み revision の v2 finalization snapshot だけを正本にし、現行の
/// Office/Recipient/Certificate/DailyRecord を読み直さない。
/// </summary>
/// <remarks>
/// <para>
/// 入力の充足性（<c>provider:*</c> の readiness）は<b>確定時</b>に
/// <c>ClaimPreparationReadiness</c> が判定済みで、CSV は確定済み snapshot からしか作らない。
/// そのため本ユースケースは readiness を再判定せず、確定後に spec 上必須の項目が空だった場合は
/// encoder が <c>MissingRequired</c> で fail-close し、項目 ID 付きで
/// <see cref="ClaimCsvExportFailedException"/> になる。
/// </para>
/// <para>
/// 失敗時は出力履歴（<see cref="ClaimCsvExport"/>）を追記しない。中途半端な出力痕跡を残さない。
/// </para>
/// </remarks>
public sealed class ExportClaimCsvUseCase(
    IClaimBatchRepository batchRepository,
    IClaimMasterProvider masterProvider,
    IClaimCsvGenerator generator,
    IClaimCsvExportRepository exportRepository,
    TimeProvider clock)
{
    /// <summary>就労継続支援B型の単位数単価を引くサービス種別キー。</summary>
    private const string ServiceKind = "employment-continuation-support";

    /// <summary>単位数単価は1/1000円単位の整数で CSV へ書く（spec の roundDown 式が要求する尺度）。</summary>
    private const int UnitPriceScale = 1000;

    public async Task<ClaimCsvExportResult> ExecuteAsync(
        Guid officeId,
        ServiceMonth serviceMonth,
        ProcessingMonth processingMonth,
        string actor,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);

        var aggregates = await batchRepository.ListHistoryAggregatesAsync(officeId, serviceMonth, ct);
        var latest = aggregates
            .Where(aggregate => aggregate.Header.Kind != RecordKind.Cancel)
            .OrderByDescending(aggregate => aggregate.Header.Revision)
            .FirstOrDefault()
            ?? throw new ClaimBatchNotFinalizedException(officeId, serviceMonth.ToString());

        if (latest.Details.Count == 0)
        {
            throw new ClaimBatchNotFinalizedException(officeId, serviceMonth.ToString());
        }

        var dto = BuildDto(latest.Header, latest.Details, serviceMonth, processingMonth);
        var bytes = generator.Generate(dto);

        var sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
        await exportRepository.AppendAsync(
            ClaimCsvExport.NewRecord(
                Guid.CreateVersion7(),
                latest.Header.Id,
                processingMonth,
                latest.Header.CsvSpecificationVersion,
                latest.Header.ClaimMasterVersion,
                sha256,
                bytes.Length,
                actor,
                clock.GetUtcNow()),
            ct);

        return new ClaimCsvExportResult(bytes, BuildFileName(dto, processingMonth, sha256), sha256);
    }

    private ClaimCsvDto BuildDto(
        ClaimBatch header,
        IReadOnlyList<ClaimDetail> details,
        ServiceMonth serviceMonth,
        ProcessingMonth processingMonth)
    {
        var snapshots = details
            .Select(detail => ClaimFinalizationSnapshotReader.Parse(
                Encoding.UTF8.GetBytes(detail.CalculationSnapshotJson)))
            .ToArray();
        var office = snapshots[0].Office;

        var recipients = details
            .Zip(snapshots, (detail, snapshot) => MapRecipient(detail, snapshot))
            .OrderBy(recipient => recipient.SortKey, StringComparer.Ordinal)
            .ToArray();

        return new ClaimCsvDto(
            processingMonth,
            serviceMonth,
            new ClaimCsvOfficeDto(
                office.OfficeNumber,
                RegionClassificationCode(office.RegionGrade),
                UnitPriceMilliYen(serviceMonth, office.RegionGrade)),
            recipients,
            new ClaimCsvTotalsDto(
                header.TotalUnits, header.TotalCostYen, header.TotalBenefitYen, header.TotalBurdenYen),
            new ClaimCsvSpecVersionDto(header.CsvSpecificationVersion, header.ClaimMasterVersion));
    }

    private static ClaimCsvRecipientDto MapRecipient(
        ClaimDetail detail,
        ClaimFinalizationSnapshot snapshot) => new(
        SortKey: snapshot.Certificate.CertificateNumber,
        CertificateNumber: snapshot.Certificate.CertificateNumber,
        MunicipalityNumber: snapshot.Certificate.MunicipalityNumber,
        SubsidyMunicipalityNumber: snapshot.Certificate.SubsidyMunicipalityNumber,
        RecipientKanaName: snapshot.Recipient.KanaName,
        MonthlyCostCapYen: snapshot.Certificate.MonthlyCostCap,
        UpperLimitManagementProviderNumber: snapshot.Certificate.UpperLimitManagementProviderNumber,
        UpperLimitManagementResultCode: ParseUpperLimitResult(snapshot.ClaimInput.UpperLimitManagementResult),
        UpperLimitManagedAmountYen: snapshot.ClaimInput.UpperLimitManagedAmountYen,
        MunicipalSubsidyAmountYen: snapshot.ClaimInput.MunicipalSubsidyAmountYen,
        ExceptionalUsageStartMonth: snapshot.ClaimInput.ExceptionalUsageStartMonth,
        ExceptionalUsageEndMonth: snapshot.ClaimInput.ExceptionalUsageEndMonth,
        ExceptionalUsageDays: snapshot.ClaimInput.ExceptionalUsageDays,
        StandardUsageDayTotal: snapshot.ClaimInput.StandardUsageDayTotal,
        IntensiveSupportEpisodeStartDate: snapshot.IntensiveSupportEpisode?.StartDate,
        ServiceLines: [.. snapshot.ClaimLines
            .OrderBy(line => line.ServiceCode, StringComparer.Ordinal)
            .Select(line => new ClaimCsvServiceLineDto(line.ServiceCode, line.Unit, line.Count))],
        DailyRecords: [.. snapshot.DailyRecords
            .OrderBy(record => record.ServiceDate)
            .Select(MapDailyRecord)],
        BilledDays: snapshot.BilledDays,
        TotalUnits: detail.TotalUnits,
        TotalCostYen: detail.TotalCostYen,
        BenefitYen: detail.BenefitYen,
        BurdenYen: detail.BurdenYen);

    private static ClaimCsvDailyRecordDto MapDailyRecord(
        ClaimFinalizationDailyRecordSnapshot record) => new(
        ServiceDate: record.ServiceDate,
        AttendanceCode: (int)record.Attendance,
        MealProvided: record.MealProvided,
        TransportCode: (int)record.Transport,
        ServiceStartTime: record.ServiceStartTime,
        ServiceEndTime: record.ServiceEndTime,
        SpecialVisitSupportMinutes: record.SpecialVisitSupportMinutes,
        OffsiteSupportApplied: record.OffsiteSupportApplied,
        MedicalCoordinationCode: ParseEnumCode<MedicalCoordinationType>(record.MedicalCoordinationType),
        TrialUseSupportCode: ParseEnumCode<TrialUseSupportType>(record.TrialUseSupportType),
        RegionalCollaborationApplied: record.RegionalCollaborationApplied,
        IntensiveSupportApplied: record.IntensiveSupportApplied,
        EmergencyAdmissionApplied: record.EmergencyAdmissionApplied);

    private static int? ParseEnumCode<TEnum>(string? token)
        where TEnum : struct, Enum =>
        token is not null && Enum.TryParse<TEnum>(token, ignoreCase: false, out var parsed)
            ? Convert.ToInt32(parsed, CultureInfo.InvariantCulture)
            : null;

    private static int? ParseUpperLimitResult(string? token) =>
        token is not null
        && Enum.TryParse<Domain.Logic.Claim.Models.UpperLimitManagementResult>(
            token, ignoreCase: false, out var parsed)
            ? (int)parsed
            : null;

    /// <summary>
    /// 地域区分コード。<c>RegionGrade.GradeN</c> は N 級地そのものなので、CSV の 2 桁コードは
    /// 級地番号のゼロ詰めとする。<c>Other</c> / <c>None</c> の公式コードは本リポジトリの
    /// 一次資料から一意に確定できないため fail-close し、<c>docs/open-questions.md</c> に起票している。
    /// </summary>
    private static string RegionClassificationCode(RegionGrade grade) => grade switch
    {
        >= RegionGrade.Grade1 and <= RegionGrade.Grade7 =>
            ((int)grade).ToString("D2", CultureInfo.InvariantCulture),
        _ => throw new ClaimCsvExportFailedException(
            "provider:J121:01:010",
            "UnknownRegionClassification",
            "the official region classification code for this grade is not determined by repository sources"),
    };

    private int UnitPriceMilliYen(ServiceMonth serviceMonth, RegionGrade grade)
    {
        var masters = masterProvider.ResolveCalculationMasters(serviceMonth);
        var regionKey = grade == RegionGrade.Other
            ? "region-other"
            : $"region-grade-{(int)grade}";
        var candidates = masters.RegionUnitPrices
            .Where(row => string.Equals(row.RegionKey, regionKey, StringComparison.Ordinal)
                && string.Equals(row.ServiceKind, ServiceKind, StringComparison.Ordinal)
                && row.EffectiveFrom <= serviceMonth
                && (row.EffectiveTo is null || serviceMonth <= row.EffectiveTo))
            .ToArray();

        return candidates.Length == 1
            ? (int)decimal.Round(candidates[0].UnitPriceYen * UnitPriceScale, 0, MidpointRounding.ToZero)
            : throw new ClaimCsvExportFailedException(
                "provider:J121:04:011",
                "UnitPriceUnresolved",
                $"{candidates.Length} unit price rows matched region '{regionKey}' for the service month");
    }

    private static string BuildFileName(
        ClaimCsvDto dto,
        ProcessingMonth processingMonth,
        string sha256) =>
        $"kokuho_{dto.Office.OfficeNumber}_{processingMonth.Year:D4}{processingMonth.Month:D2}_{sha256[..8]}.csv";
}
