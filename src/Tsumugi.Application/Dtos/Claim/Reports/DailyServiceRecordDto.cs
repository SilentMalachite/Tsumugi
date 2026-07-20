using Tsumugi.Domain.Enums;

namespace Tsumugi.Application.Dtos.Claim.Reports;

/// <summary>サービス提供実績記録票の日次1行分（spec §7.4）。</summary>
public sealed record DailyServiceRecordDto(
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
