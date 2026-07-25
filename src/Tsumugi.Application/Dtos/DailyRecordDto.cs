using Tsumugi.Domain.Enums;

namespace Tsumugi.Application.Dtos;

public sealed record DailyRecordDto(
    Guid Id, Guid RecipientId, DateOnly ServiceDate,
    RecordKind Kind, Guid? OriginId,
    Attendance Attendance, TransportKind Transport, bool MealProvided, string? Note)
{
    public TimeOnly? ServiceStartTime { get; init; }
    public TimeOnly? ServiceEndTime { get; init; }
    public int? SpecialVisitSupportMinutes { get; init; }

    /// <summary>
    /// 訪問支援特別加算の算定時間数（単位は「時間」・整数）。実際のサービス提供時間を分で持つ
    /// <see cref="SpecialVisitSupportMinutes"/> とは別項目で、そこからは導出できない。
    /// </summary>
    public int? SpecialVisitSupportBilledHours { get; init; }

    public bool? OffsiteSupportApplied { get; init; }
    public MedicalCoordinationType MedicalCoordinationType { get; init; }
    public TrialUseSupportType TrialUseSupportType { get; init; }
    public bool? RegionalCollaborationApplied { get; init; }
    public bool? IntensiveSupportApplied { get; init; }
    public bool? EmergencyAdmissionApplied { get; init; }
    public RecipientConfirmationStatus RecipientConfirmation { get; init; }
}
