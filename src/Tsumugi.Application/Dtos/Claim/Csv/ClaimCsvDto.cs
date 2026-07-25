using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Dtos.Claim.Csv;

/// <summary>
/// 国保連請求CSVの生成入力。確定済み <c>ClaimBatch</c> の finalization snapshot v2 と、
/// 独立入力の <see cref="ProcessingMonth"/>、および制度マスタから解決済みの
/// 地域区分・単位数単価だけで構成する。生成側（Infrastructure.Csv）は
/// EF Core・制度マスタ・現行エンティティを一切参照しない。
/// </summary>
/// <param name="Recipients">
/// 決定論のため <see cref="ClaimCsvRecipientDto.SortKey"/> の序数昇順で並べて渡す。
/// </param>
public sealed record ClaimCsvDto(
    ProcessingMonth ProcessingMonth,
    ServiceMonth ServiceMonth,
    ClaimCsvOfficeDto Office,
    IReadOnlyList<ClaimCsvRecipientDto> Recipients,
    ClaimCsvTotalsDto Totals,
    ClaimCsvSpecVersionDto SpecVersion);

/// <param name="RegionClassificationCode">地域区分コード（制度マスタ解決済み）。</param>
/// <param name="UnitPriceMilliYen">
/// 単位数単価を1/1000円単位で表した整数（例: 10.00円/単位 → 10000）。
/// spec の <c>provider:J121:04:013 = roundDown(010 * 011 / 1000)</c> がこの尺度を要求する。
/// </param>
public sealed record ClaimCsvOfficeDto(
    string OfficeNumber,
    string RegionClassificationCode,
    int UnitPriceMilliYen);

/// <param name="SortKey">受給者の安定ソートキー（受給者証番号）。決定論の要。</param>
/// <param name="UpperLimitManagementResultCode">上限管理結果の公式コード値（1/2/3）。</param>
public sealed record ClaimCsvRecipientDto(
    string SortKey,
    string CertificateNumber,
    string MunicipalityNumber,
    string? SubsidyMunicipalityNumber,
    string RecipientKanaName,
    int MonthlyCostCapYen,
    string? UpperLimitManagementProviderNumber,
    int? UpperLimitManagementResultCode,
    int? UpperLimitManagedAmountYen,
    int? MunicipalSubsidyAmountYen,
    ServiceMonth? ExceptionalUsageStartMonth,
    ServiceMonth? ExceptionalUsageEndMonth,
    int? ExceptionalUsageDays,
    int? StandardUsageDayTotal,
    DateOnly? IntensiveSupportEpisodeStartDate,
    ClaimCsvContractDto? Contract,
    IReadOnlyList<ClaimCsvServiceLineDto> ServiceLines,
    IReadOnlyList<ClaimCsvDailyRecordDto> DailyRecords,
    int BilledDays,
    int TotalUnits,
    int TotalCostYen,
    int BenefitYen,
    int BurdenYen);

/// <summary>
/// 明細書「契約情報」レコード（<c>provider:J121:05</c>）に必要な契約内容。
/// 確定時点の契約事業所情報であり、CSV 生成時に現行エンティティを読み直さない。
/// </summary>
/// <param name="FirstServiceDate">
/// 有効な継続契約における最初のサービス提供日。当月の日次記録からは復元できないため、
/// 確定時点の値を持ち回る。
/// </param>
public sealed record ClaimCsvContractDto(
    int ContractedSupplyDays,
    DateOnly ContractDate,
    DateOnly? TerminationDate,
    int CertificateEntryNumber,
    DateOnly FirstServiceDate);

/// <param name="Unit">1回あたり単位数。</param>
/// <param name="Count">回数。</param>
public sealed record ClaimCsvServiceLineDto(string ServiceCode, int Unit, int Count);

/// <param name="AttendanceCode">出欠の内部区分値（<c>Attendance</c> の数値）。</param>
/// <param name="TransportCode">送迎の内部区分値（<c>TransportKind</c> の数値）。</param>
/// <param name="MedicalCoordinationCode">医療連携体制加算の内部区分値。未設定は null。</param>
/// <param name="TrialUseSupportCode">体験利用支援加算の内部区分値。未設定は null。</param>
public sealed record ClaimCsvDailyRecordDto(
    DateOnly ServiceDate,
    int AttendanceCode,
    bool MealProvided,
    int TransportCode,
    TimeOnly? ServiceStartTime,
    TimeOnly? ServiceEndTime,
    int? SpecialVisitSupportMinutes,
    bool OffsiteSupportApplied,
    int? MedicalCoordinationCode,
    int? TrialUseSupportCode,
    bool RegionalCollaborationApplied,
    bool IntensiveSupportApplied,
    bool EmergencyAdmissionApplied);

public sealed record ClaimCsvTotalsDto(
    int TotalUnits,
    int TotalCostYen,
    int TotalBenefitYen,
    int TotalBurdenYen);

public sealed record ClaimCsvSpecVersionDto(
    string CsvSpecificationVersion,
    string ClaimMasterVersion);
