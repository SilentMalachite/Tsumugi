using System.Text;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Claim;
using Tsumugi.Application.Dtos.Claim.Reports;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.UseCases.Claim;

/// <summary>
/// 3帳票（サービス提供実績記録票／請求書／請求明細書）のconsumer側orchestration（spec §9）。
/// <see cref="IClaimBatchRepository"/>だけを参照し、確定済みrevisionのv2 finalization snapshot
/// （<see cref="ClaimFinalizationSnapshotReader"/>）をparseして各帳票DTOへ写像し、
/// <see cref="IClaimReportGenerator"/>へ委譲する。Office/Recipient/Certificate/DailyRecordを
/// 再読込しない（現行のDailyRecord等を参照するとrevision確定時点の帳票と食い違うため、
/// 確定時点でsnapshotへ焼き込んだ値だけを正本として使う）。
/// </summary>
public sealed class GenerateClaimReportsUseCase(
    IClaimBatchRepository claimBatchRepository,
    IClaimReportGenerator generator)
{
    public async Task<byte[]> GenerateServiceProvisionRecordAsync(
        Guid officeId, ServiceMonth serviceMonth, Guid recipientId, CancellationToken ct)
    {
        var (_, details) = await ResolveLatestConfirmedAsync(officeId, serviceMonth, ct);
        var detail = details.FirstOrDefault(d => d.RecipientId == recipientId)
            ?? throw new InvalidOperationException(
                $"受給者 {recipientId} は確定revision {officeId}×{serviceMonth} に見つかりません。");

        var snapshot = ParseSnapshot(detail);
        var dto = new ServiceProvisionRecordDto(
            Office: MapOffice(snapshot.Office),
            Recipient: MapRecipient(snapshot.Recipient),
            Certificate: MapCertificate(snapshot.Certificate),
            YearMonth: ToYearMonth(serviceMonth),
            Days: [.. snapshot.DailyRecords.Select(MapDailyRecord)],
            IntensiveSupport: MapIntensiveSupport(snapshot.IntensiveSupportEpisode),
            SpecVersion: MapSpecVersion(snapshot));

        return generator.GenerateServiceProvisionRecord(dto);
    }

    public async Task<byte[]> GenerateClaimInvoiceAsync(
        Guid officeId, ServiceMonth serviceMonth, CancellationToken ct)
    {
        var (header, details) = await ResolveLatestConfirmedAsync(officeId, serviceMonth, ct);
        RequireDetails(details, officeId, serviceMonth);

        var firstSnapshot = ParseSnapshot(details[0]);
        var dto = new ClaimInvoiceDto(
            Office: MapOffice(firstSnapshot.Office),
            YearMonth: ToYearMonth(serviceMonth),
            TotalUnit: header.TotalUnits,
            TotalCostYen: header.TotalCostYen,
            TotalBenefitYen: header.TotalBenefitYen,
            TotalBurdenYen: header.TotalBurdenYen,
            SpecVersion: MapSpecVersion(firstSnapshot));

        return generator.GenerateClaimInvoice(dto);
    }

    public async Task<byte[]> GenerateClaimStatementAsync(
        Guid officeId, ServiceMonth serviceMonth, CancellationToken ct)
    {
        var (header, details) = await ResolveLatestConfirmedAsync(officeId, serviceMonth, ct);
        RequireDetails(details, officeId, serviceMonth);

        // 各detailのsnapshotはrecipients写像とOffice抽出の両方で必要になるため、1回のparseで両方に使う
        // （同一bytesの二重parseを避ける）。
        var entries = details.Select(detail => (Detail: detail, Snapshot: ParseSnapshot(detail))).ToArray();
        var recipients = entries
            .Select(entry => new RecipientClaimDetailDto(
                Recipient: MapRecipient(entry.Snapshot.Recipient),
                Certificate: MapCertificate(entry.Snapshot.Certificate),
                Lines: [.. entry.Snapshot.ClaimLines.Select(MapClaimLine)],
                SubtotalUnit: entry.Detail.TotalUnits,
                SubtotalCostYen: entry.Detail.TotalCostYen,
                SubtotalBenefitYen: entry.Detail.BenefitYen,
                SubtotalBurdenYen: entry.Detail.BurdenYen,
                ClaimInput: new ClaimInputSummaryDto(
                    entry.Snapshot.ClaimInput.UpperLimitManagementResult,
                    entry.Snapshot.ClaimInput.UpperLimitManagedAmountYen,
                    entry.Snapshot.ClaimInput.MunicipalSubsidyAmountYen)))
            .ToArray();

        var firstSnapshot = entries[0].Snapshot;
        var dto = new ClaimStatementDto(
            Office: MapOffice(firstSnapshot.Office),
            YearMonth: ToYearMonth(serviceMonth),
            Recipients: recipients,
            TotalUnit: header.TotalUnits,
            TotalCostYen: header.TotalCostYen,
            TotalBenefitYen: header.TotalBenefitYen,
            TotalBurdenYen: header.TotalBurdenYen,
            SpecVersion: MapSpecVersion(firstSnapshot));

        return generator.GenerateClaimStatement(dto);
    }

    /// <summary>
    /// 「最新確定revision」＝ 履歴中<see cref="RecordKind.Cancel"/>を除いたRevision最大値。
    /// 履歴が空、または全件Cancelの場合はfail-closedで例外にする。
    /// </summary>
    private async Task<(ClaimBatch Header, IReadOnlyList<ClaimDetail> Details)> ResolveLatestConfirmedAsync(
        Guid officeId, ServiceMonth serviceMonth, CancellationToken ct)
    {
        var aggregates = await claimBatchRepository.ListHistoryAggregatesAsync(officeId, serviceMonth, ct);
        var latest = aggregates
            .Where(aggregate => aggregate.Header.Kind != RecordKind.Cancel)
            .OrderByDescending(aggregate => aggregate.Header.Revision)
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"{officeId}×{serviceMonth} に確定revisionが存在しません。");

        return (latest.Header, latest.Details);
    }

    private static void RequireDetails(IReadOnlyList<ClaimDetail> details, Guid officeId, ServiceMonth serviceMonth)
    {
        if (details.Count == 0)
            throw new InvalidOperationException(
                $"確定revision {officeId}×{serviceMonth} に受給者detailが存在しません。");
    }

    // canonical JSON snapshotのparserでculture依存の数値/日付書式は扱わない
    // （内部のDateOnly/TimeOnly/intは全てCultureInfo.InvariantCultureを明示済み。
    // CultureExplicitnessGuardTestsの対象外である旨をここに明記する）。
    private static ClaimFinalizationSnapshot ParseSnapshot(ClaimDetail detail) => ClaimFinalizationSnapshotReader
        .Parse(Encoding.UTF8.GetBytes(detail.CalculationSnapshotJson)); // CultureInfo: 非該当（JSON snapshot parser）

    private static YearMonth ToYearMonth(ServiceMonth serviceMonth) => new(serviceMonth.Year, serviceMonth.Month);

    private static ClaimReportOfficeDto MapOffice(ClaimFinalizationOfficeSnapshot office) => new(
        office.OfficeNumber,
        office.OfficeName,
        office.RegionGrade,
        office.PostalCode,
        office.Address,
        office.PhoneNumber,
        office.RepresentativeTitleAndName);

    private static ClaimReportRecipientDto MapRecipient(ClaimFinalizationRecipientSnapshot recipient) => new(
        recipient.KanjiName,
        recipient.KanaName);

    private static ClaimReportCertificateDto MapCertificate(ClaimFinalizationCertificateSnapshot certificate) => new(
        certificate.CertificateNumber,
        certificate.MunicipalityNumber,
        certificate.SubsidyMunicipalityNumber,
        certificate.MonthlyCostCap,
        certificate.UpperLimitManagementProviderNumber,
        certificate.UpperLimitManagementProviderName);

    private static ClaimReportSpecVersionDto MapSpecVersion(ClaimFinalizationSnapshot snapshot) => new(
        snapshot.ClaimMasterVersion,
        snapshot.CsvSpecificationVersion,
        snapshot.ReportSpecificationVersion);

    private static DailyServiceRecordDto MapDailyRecord(ClaimFinalizationDailyRecordSnapshot record) => new(
        record.ServiceDate,
        record.Attendance,
        record.MealProvided,
        record.Transport,
        record.AbsenceResponseNote,
        record.ServiceStartTime,
        record.ServiceEndTime,
        record.SpecialVisitSupportMinutes,
        record.OffsiteSupportApplied,
        record.MedicalCoordinationType,
        record.TrialUseSupportType,
        record.RegionalCollaborationApplied,
        record.IntensiveSupportApplied,
        record.EmergencyAdmissionApplied,
        record.RecipientConfirmation);

    private static IntensiveSupportEpisodeDto? MapIntensiveSupport(
        ClaimFinalizationIntensiveSupportEpisodeSnapshot? episode)
        => episode is null ? null : new IntensiveSupportEpisodeDto(episode.StartDate);

    private static ClaimLineDto MapClaimLine(ClaimFinalizationClaimLineSnapshot line) => new(
        line.Kind,
        line.ServiceCode,
        line.Unit,
        line.Count,
        line.AmountYen);
}
