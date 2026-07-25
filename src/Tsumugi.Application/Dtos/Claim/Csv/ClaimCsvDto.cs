using Tsumugi.Domain.Enums;
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

/// <param name="RegionGrade">
/// 地域区分。CSV の公式コードへの変換は CSV 仕様側（<c>Tsumugi.Infrastructure.Csv</c>）が行う。
/// コードは級地番号のゼロ詰めではないため、Application で組み立てない。
/// </param>
/// <param name="UnitPriceMilliYen">
/// 単位数単価を1/1000円単位で表した整数（例: 10.00円/単位 → 10000）。共通編 1.5.1(4)
/// 「単位数単価」欄が整数部2桁・小数部3桁を要求するため、この尺度が公式仕様と一致する。
/// </param>
public sealed record ClaimCsvOfficeDto(
    string OfficeNumber,
    RegionGrade RegionGrade,
    int UnitPriceMilliYen);

/// <param name="SortKey">受給者の安定ソートキー（受給者証番号）。決定論の要。</param>
/// <param name="BilledDays">
/// 本体報酬を算定した日数。明細書 契約情報レコードの「利用日数」（<c>provider:J121:02:010</c>）は
/// 事業所編③により<b>欠席時対応加算のみの日を除く</b>ため、この値を用いる。
/// </param>
/// <param name="ServiceUsageDays">
/// 明細書 集計欄の「サービス利用日数」（<c>provider:J121:04:009</c>）。事業所編の項目説明により
/// <b>欠席時対応加算のみの日も 1 日として数える</b>ため <paramref name="BilledDays"/> とは別の値。
/// 確定 snapshot が持たない場合（Phase 3-3 より前の確定分）は null で、生成側が fail-close する。
/// </param>
/// <param name="UpperLimitManagementResultCode">上限管理結果の公式コード値（1/2/3）。</param>
/// <param name="SpecialVisitSupportBilledCount">
/// 訪問支援特別加算の算定回数（当月合計・単位は「回」）。日次のサービス提供時間からは導出できない
/// 個別入力で、確定 snapshot が持たない場合（Phase 3-3 より前の確定分）は null。
/// </param>
/// <param name="OffsiteSupportCumulativeDays">
/// 施設外支援の累計日数（単位は「日」）。当月分を含むかは公式資料から一意に確定できないため、
/// 運用者が明細書の「累計」欄に設定した値をそのまま渡す（アプリで導出しない）。
/// 確定 snapshot が持たない場合（Phase 3-3 より前の確定分）は null。
/// </param>
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
    int? ServiceUsageDays,
    int TotalUnits,
    int TotalCostYen,
    int BenefitYen,
    int BurdenYen,
    // Phase 3-3（グループB個別入力）。既存プロパティの順序を変えるとゴールデンCSVが壊れるため、
    // 末尾に省略可能パラメータとして追記する。
    int? SpecialVisitSupportBilledCount = null,
    int? OffsiteSupportCumulativeDays = null,
    /// <summary>
    /// 汎用 pass-through 入力（ADR 0042）。名前→値。CSV 仕様が <c>storage: "generic"</c> と
    /// 宣言した項目の値で、転記専用。
    /// </summary>
    IReadOnlyDictionary<string, string>? GenericInputs = null);

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
    int? CertificateEntryNumber,
    DateOnly? FirstServiceDate);

/// <param name="Unit">1回あたり単位数。</param>
/// <param name="Count">回数。</param>
public sealed record ClaimCsvServiceLineDto(string ServiceCode, int Unit, int Count);

/// <param name="AttendanceCode">出欠の内部区分値（<c>Attendance</c> の数値）。</param>
/// <param name="TransportCode">送迎の内部区分値（<c>TransportKind</c> の数値）。</param>
/// <param name="MedicalCoordinationCode">医療連携体制加算の内部区分値。未設定は null。</param>
/// <param name="TrialUseSupportCode">体験利用支援加算の内部区分値。未設定は null。</param>
/// <param name="SpecialVisitSupportBilledHours">
/// 訪問支援特別加算の算定時間数（単位は「時間」・整数）。実際のサービス提供時間を分で持つ
/// <paramref name="SpecialVisitSupportMinutes"/> とは別項目で、そこからは導出できない。
/// 確定 snapshot が持たない場合（Phase 3-3 より前の確定分）は null。
/// </param>
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
    bool EmergencyAdmissionApplied,
    // Phase 3-3（グループB個別入力）。既存プロパティの順序を変えるとゴールデンCSVが壊れるため、
    // 末尾に省略可能パラメータとして追記する。
    int? SpecialVisitSupportBilledHours = null);

public sealed record ClaimCsvTotalsDto(
    int TotalUnits,
    int TotalCostYen,
    int TotalBenefitYen,
    int TotalBurdenYen);

public sealed record ClaimCsvSpecVersionDto(
    string CsvSpecificationVersion,
    string ClaimMasterVersion);
