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
    IClaimCsvOfficeContextProvider officeContextProvider,
    IClaimCsvGenerator generator,
    IClaimCsvExportRepository exportRepository,
    TimeProvider clock)
{
    public async Task<ClaimCsvExportResult> ExecuteAsync(
        Guid officeId,
        ServiceMonth serviceMonth,
        ProcessingMonth processingMonth,
        string actor,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);

        var aggregates = await batchRepository.ListHistoryAggregatesAsync(officeId, serviceMonth, ct);
        // head は Cancel を含む最大 Revision（Domain の ClaimBatchPolicy.Head と同じ規則）。
        // Cancel を除いてから最大を採ると、取消済みの請求を過去 revision から復活させてしまう。
        var head = aggregates
            .OrderByDescending(aggregate => aggregate.Header.Revision)
            .FirstOrDefault()
            ?? throw new ClaimBatchNotFinalizedException(officeId, serviceMonth.ToString());

        if (head.Header.Kind == RecordKind.Cancel || head.Details.Count == 0)
        {
            throw new ClaimBatchNotFinalizedException(officeId, serviceMonth.ToString());
        }

        var latest = head;

        var dto = BuildDto(latest.Header, latest.Details, serviceMonth, processingMonth);
        // 確定時に記録した CSV 仕様版と、生成に使う仕様版が一致しないと、同じ確定請求から
        // 別のバイト列が出る。版が動いたことに気付かないまま出力しない。
        if (!string.Equals(
                latest.Header.CsvSpecificationVersion, generator.SpecificationVersion, StringComparison.Ordinal))
        {
            throw new ClaimCsvExportFailedException(
                fieldId: string.Empty,
                reason: "CsvSpecificationVersionMismatch",
                detail: "the finalized CSV specification version differs from the one available at export time");
        }

        var document = generator.Generate(dto);
        var bytes = document.Bytes;

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

        return new ClaimCsvExportResult(bytes, document.FileName, sha256);
    }

    private ClaimCsvDto BuildDto(
        ClaimBatch header,
        IReadOnlyList<ClaimDetail> details,
        ServiceMonth serviceMonth,
        ProcessingMonth processingMonth)
    {
        var snapshots = details
            .Select(detail => ClaimFinalizationSnapshotReader.Parse( // CultureInfo: 非該当（JSON snapshot parser）
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
            BuildOffice(office, serviceMonth),
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
        // 契約情報（provider:J121:05 / 開始年月日）は確定時点の「サービス事業者記入欄」から採る。
        // 受給者証に自事業所の行が無い、または初回サービス提供日が未入力なら null のまま渡し、
        // 必須項目として fail-close させる（推測で埋めない）。
        Contract: snapshot.ContractedProvider is { } contract
            ? new ClaimCsvContractDto(
                contract.ContractedSupplyDays,
                contract.ContractDate,
                contract.TerminationDate,
                contract.CertificateEntryNumber,
                contract.FirstServiceDate)
            : null,
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

    private ClaimCsvOfficeDto BuildOffice(
        ClaimFinalizationOfficeSnapshot office,
        ServiceMonth serviceMonth)
    {
        var context = officeContextProvider.Resolve(office.RegionGrade, serviceMonth);
        return new ClaimCsvOfficeDto(office.OfficeNumber, office.RegionGrade, context.UnitPriceMilliYen);
    }

}
