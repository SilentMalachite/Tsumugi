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
    public bool? OffsiteSupportApplied { get; init; }
    public MedicalCoordinationType MedicalCoordinationType { get; init; }
    public TrialUseSupportType TrialUseSupportType { get; init; }
    public bool? RegionalCollaborationApplied { get; init; }
    public bool? IntensiveSupportApplied { get; init; }
    public bool? EmergencyAdmissionApplied { get; init; }
    public RecipientConfirmationStatus RecipientConfirmation { get; init; }
}
