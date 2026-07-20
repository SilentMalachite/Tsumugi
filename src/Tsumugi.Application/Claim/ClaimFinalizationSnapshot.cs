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
    int BurdenYen);

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

// TODO(task 9): move ClaimDetailLineKind to Tsumugi.Application.Dtos.Claim.Reports and have report
// renderers (tasks 10-12) reference the shared enum from there instead of Tsumugi.Application.Claim.
public enum ClaimDetailLineKind
{
    Basic,
    Addition,
}

public sealed record ClaimFinalizationClaimLineSnapshot(
    ClaimDetailLineKind Kind,
    string ServiceCode,
    int Unit,
    int Count,
    int AmountYen);
