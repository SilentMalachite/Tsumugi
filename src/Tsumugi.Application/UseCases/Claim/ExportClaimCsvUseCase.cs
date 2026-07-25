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
    VerifiedClaimBatchProvider verifiedBatchProvider,
    IClaimCsvOfficeContextProvider officeContextProvider,
    IClaimCsvSpecificationVersions specificationVersions,
    IClaimInputRequirementProvider requirementProvider,
    IClaimGenericFieldCatalog genericFieldCatalog,
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

        // 実効 revision の解決（Cancel を含む最大 Revision）と履歴・envelope・payload hash・版・
        // 合計の検証は VerifiedClaimBatchProvider が行う。ここで raw aggregate を直接読むと
        // 改竄・破損した履歴から請求データを作ってしまう。
        var latest = await verifiedBatchProvider.FindEffectiveAsync(officeId, serviceMonth, ct)
            ?? throw new ClaimBatchNotFinalizedException(officeId, serviceMonth.ToString());

        // 出力に使う仕様版は「処理対象年月に適用される版」（施行分は提出時点で決まる）。
        // 該当版が無ければ推測で現行版を使わず fail-close する。
        var resolvedVersion = ResolveVersion(processingMonth);

        // 確定時の版と解決版が違っても、ここでは止めない（ADR 0040）。新しい施行分が項目を増やして
        // いなければ確定済み snapshot のままで出せるため、入口で塞ぐと不要な再確定を強いる。
        // 新版が snapshot に無いデータを要求する場合は、生成器と encoder が項目単位で fail-close する
        // （不足項目の一覧は ValidateAsync が返す）。使った版は出力履歴に記録する。
        var dto = BuildDto(
            latest.Header, latest.Details, serviceMonth, processingMonth, resolvedVersion);

        var document = generator.Generate(dto);
        var bytes = document.Bytes;

        var sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
        await exportRepository.AppendAsync(
            ClaimCsvExport.NewRecord(
                Guid.CreateVersion7(),
                latest.Header.Id,
                processingMonth,
                // 出力履歴には「実際に使った版」と「確定時の版」の両方を記録する。
                resolvedVersion,
                latest.Header.ClaimMasterVersion,
                sha256,
                bytes.Length,
                actor,
                clock.GetUtcNow(),
                latest.Header.CsvSpecificationVersion),
            ct);

        return new ClaimCsvExportResult(bytes, document.FileName, sha256);
    }

    /// <summary>
    /// この確定請求を、処理対象年月に適用される版で出力できるかを調べる。返る一覧が空なら出力できる。
    /// 出力を試して最初の1件で落とすのではなく、<b>不足している項目を全件</b>返す（ADR 0040）。
    /// </summary>
    public async Task<ClaimCsvExportValidationResult> ValidateAsync(
        Guid officeId,
        ServiceMonth serviceMonth,
        ProcessingMonth processingMonth,
        CancellationToken ct)
    {
        var latest = await verifiedBatchProvider.FindEffectiveAsync(officeId, serviceMonth, ct)
            ?? throw new ClaimBatchNotFinalizedException(officeId, serviceMonth.ToString());
        var resolvedVersion = ResolveVersion(processingMonth);
        var dto = BuildDto(
            latest.Header, latest.Details, serviceMonth, processingMonth, resolvedVersion);

        // 2 つの由来を合わせる。
        // (1) 要件由来: 解決版の readiness 要件を確定 snapshot で評価する（項目の入力漏れ）。
        // (2) 生成由来: 実際に生成を試して encoder が落ちた項目（桁・文字種・解決不能な rule）。
        // 同じ項目が両方から出ることがあるため fieldId で重複排除する。要件由来の issue は
        // モデル path（ContractedProvider.FirstServiceDate 等）を持つので、<b>仕様上の fieldId へ展開</b>
        // してから合流させる（path のまま載せると DTO 契約に反し、生成由来の同一不足と重複排除されない）。
        var requirements = requirementProvider.GetRequirements(resolvedVersion);
        var fieldIdsByTargetPath = requirements.ToDictionary(
            requirement => requirement.TargetPath,
            requirement => (IReadOnlyList<string>)requirement.FieldIds,
            StringComparer.Ordinal);
        var requirementIssues = latest.Details
            .Select(detail => ClaimFinalizationSnapshotReader.Parse( // CultureInfo: 非該当（JSON snapshot parser）
                Encoding.UTF8.GetBytes(detail.CalculationSnapshotJson)))
            .SelectMany(snapshot => ClaimFinalizationReadinessContextBuilder.Evaluate(
                snapshot,
                requirements,
                [.. genericFieldCatalog.GetDeclarations(resolvedVersion)
                    .Select(declaration => declaration.Name)]))
            .SelectMany(issue => FieldIdsOf(issue, fieldIdsByTargetPath)
                .Select(fieldId => new ClaimCsvFieldIssue(
                    fieldId,
                    issue.Code.ToString(),
                    "確定済み請求に、この仕様版が要求する項目が入っていません。",
                    null)));

        var issues = requirementIssues
            .Concat(generator.CollectIssues(dto))
            .GroupBy(issue => issue.FieldId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();

        return new ClaimCsvExportValidationResult(
            latest.Header.CsvSpecificationVersion, resolvedVersion, issues);
    }

    /// <summary>
    /// 要件由来の issue を仕様上の fieldId へ展開する。要件は 1 つの target path に対して複数の項目を
    /// 束ねているため、不足は<b>その全項目</b>として示す（利用者が仕様書で引ける ID になる）。
    /// 対応する要件が見つからない場合だけは path を落とさずそのまま載せる（黙って消さない）。
    /// </summary>
    private static IReadOnlyList<string> FieldIdsOf(
        ClaimPreparationIssue issue,
        Dictionary<string, IReadOnlyList<string>> fieldIdsByTargetPath)
        => fieldIdsByTargetPath.TryGetValue(issue.FieldCode, out var fieldIds) && fieldIds.Count > 0
            ? fieldIds
            : [issue.FieldCode];

    private string ResolveVersion(ProcessingMonth processingMonth)
    {
        try
        {
            return specificationVersions.ResolveForProcessingMonth(processingMonth);
        }
        catch (InvalidOperationException exception)
        {
            throw new ClaimCsvExportFailedException(
                fieldId: string.Empty,
                reason: "CsvSpecificationVersionUnavailable",
                detail: exception.Message);
        }
    }

    private ClaimCsvDto BuildDto(
        ClaimBatch header,
        IReadOnlyList<ClaimDetail> details,
        ServiceMonth serviceMonth,
        ProcessingMonth processingMonth,
        string csvSpecificationVersion)
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
            new ClaimCsvSpecVersionDto(csvSpecificationVersion, header.ClaimMasterVersion));
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
        ServiceUsageDays: snapshot.ServiceUsageDays,
        TotalUnits: detail.TotalUnits,
        TotalCostYen: detail.TotalCostYen,
        BenefitYen: detail.BenefitYen,
        BurdenYen: detail.BurdenYen,
        // グループB個別入力（訪問支援特別加算の算定回数・施設外支援の累計日数）。確定時点の値を
        // そのまま渡し、必須判定は CSV 仕様側の fail-close に委ねる（推測で埋めない）。
        SpecialVisitSupportBilledCount: snapshot.ClaimInput.SpecialVisitSupportBilledCount,
        OffsiteSupportCumulativeDays: snapshot.ClaimInput.OffsiteSupportCumulativeDays,
        GenericInputs: snapshot.ClaimInput.GenericValues.ToDictionary(
            value => value.Name, value => value.Value, StringComparer.Ordinal));

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
        EmergencyAdmissionApplied: record.EmergencyAdmissionApplied,
        SpecialVisitSupportBilledHours: record.SpecialVisitSupportBilledHours);

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
