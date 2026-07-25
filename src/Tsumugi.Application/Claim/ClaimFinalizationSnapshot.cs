using Tsumugi.Application.Dtos.Claim.Reports;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Claim;

/// <summary>
/// 確定時点の受給者スナップショット全体（spec §6 snapshot v2 schema, snapshotKind = "finalization"）。
/// 3帳票（サービス提供実績記録票／請求書／請求明細書）を、現行のDailyRecord/Certificate/Office等を
/// 再読込せずに、このスナップショットのみから決定論的にレンダリングできることを保証する契約。
/// <see cref="ClaimFinalizationSnapshotWriter"/>/<see cref="ClaimFinalizationSnapshotReader"/>が
/// canonical JSON との相互変換を担う。
/// </summary>
public sealed record ClaimFinalizationSnapshot(
    Guid RecipientId,
    ServiceMonth ServiceMonth,
    string ClaimMasterVersion,
    string CsvSpecificationVersion,
    string ReportSpecificationVersion,
    ClaimFinalizationOfficeSnapshot Office,
    ClaimFinalizationRecipientSnapshot Recipient,
    ClaimFinalizationCertificateSnapshot Certificate,
    ClaimFinalizationClaimInputSnapshot ClaimInput,
    IReadOnlyList<ClaimFinalizationDailyRecordSnapshot> DailyRecords,
    ClaimFinalizationIntensiveSupportEpisodeSnapshot? IntensiveSupportEpisode,
    IReadOnlyList<ClaimFinalizationClaimLineSnapshot> ClaimLines,
    int BilledDays,
    int TotalUnits,
    int TotalCostYen,
    int BenefitYen,
    int BurdenYen,
    /// <summary>
    /// 確定時点の「サービス事業者記入欄」（自事業所との契約）。Phase 3-3 で追加したため、
    /// これより前に確定した snapshot では null になる。canonical JSON 上の位置は Writer が決める。
    /// </summary>
    ClaimFinalizationContractedProviderSnapshot? ContractedProvider = null);

public sealed record ClaimFinalizationOfficeSnapshot(
    string OfficeNumber,
    string OfficeName,
    RegionGrade RegionGrade,
    string PostalCode,
    string Address,
    string PhoneNumber,
    string RepresentativeTitleAndName);

public sealed record ClaimFinalizationRecipientSnapshot(string KanjiName, string KanaName);

public sealed record ClaimFinalizationCertificateSnapshot(
    string CertificateNumber,
    string MunicipalityNumber,
    string? SubsidyMunicipalityNumber,
    int MonthlyCostCap,
    string? UpperLimitManagementProviderNumber,
    string? UpperLimitManagementProviderName);

/// <summary>
/// 確定時点の「サービス事業者記入欄」（自事業所との契約）。明細書 契約情報レコード
/// （<c>provider:J121:05</c>）と開始年月日（<c>provider:J121:02:008</c>）の正本。
/// 自事業所の行が受給者証に記録されていない場合は null で、CSV 生成側が fail-close する。
/// </summary>
public sealed record ClaimFinalizationContractedProviderSnapshot(
    int ContractedSupplyDays,
    DateOnly ContractDate,
    DateOnly? TerminationDate,
    int? CertificateEntryNumber,
    DateOnly? FirstServiceDate);

public sealed record ClaimFinalizationClaimInputSnapshot(
    string? UpperLimitManagementResult,
    int? UpperLimitManagedAmountYen,
    int? MunicipalSubsidyAmountYen,
    ServiceMonth? ExceptionalUsageStartMonth,
    ServiceMonth? ExceptionalUsageEndMonth,
    int? ExceptionalUsageDays,
    int? StandardUsageDayTotal);

public sealed record ClaimFinalizationDailyRecordSnapshot(
    DateOnly ServiceDate,
    Attendance Attendance,
    bool MealProvided,
    TransportKind Transport,
    string? AbsenceResponseNote,
    TimeOnly? ServiceStartTime,
    TimeOnly? ServiceEndTime,
    int? SpecialVisitSupportMinutes,
    bool OffsiteSupportApplied,
    string? MedicalCoordinationType,
    string? TrialUseSupportType,
    bool RegionalCollaborationApplied,
    bool IntensiveSupportApplied,
    bool EmergencyAdmissionApplied,
    bool RecipientConfirmation);

public sealed record ClaimFinalizationIntensiveSupportEpisodeSnapshot(DateOnly StartDate);

/// <param name="AmountYen">
/// 行単位の金額（円）。<b>導出値であり、正本ではない</b>
/// （<see cref="OperationLocalSnapshotReader.BuildClaimLines"/>のXML docに算出根拠の詳細がある）。
/// このReaderには地域単価（decimal）が渡されず再算定もしないため、行ごとの真の金額を一意に復元
/// できない。<c>TotalCostYen / TotalUnits</c>の近似単価を用い、加算行は
/// <c>Math.Floor(Unit × 近似単価)</c>で概算し、残余を基本報酬行（Kind=Basic）へ寄せることで
/// <c>Σ(ClaimLines[].AmountYen) == TotalCostYen</c>の整合性のみを保証する（表示用の近似値）。
/// 制度上の金額確定・国保連への反映は本フィールドではなく<c>TotalCostYen</c>/<c>BenefitYen</c>/
/// <c>BurdenYen</c>（<see cref="ClaimFinalizationSnapshot"/>直下のrecipient集計値）が正本。
/// </param>
public sealed record ClaimFinalizationClaimLineSnapshot(
    ClaimDetailLineKind Kind,
    string ServiceCode,
    int Unit,
    int Count,
    int AmountYen);
