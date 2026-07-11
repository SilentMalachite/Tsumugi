using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.Validation;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Application.UseCases.DailyRecord;

public sealed class RecordDailyRecordUseCase(
    IDailyRecordRepository repo, IUnitOfWork uow, TimeProvider clock)
{
    public async Task<DailyRecordDto> ExecuteAsync(
        Guid recipientId, DateOnly serviceDate,
        Attendance attendance, TransportKind transport, bool mealProvided, string? note,
        string actor, CancellationToken ct)
        => await ExecuteAsync(
            recipientId, serviceDate, attendance, transport, mealProvided, note,
            serviceStartTime: null,
            serviceEndTime: null,
            specialVisitSupportMinutes: null,
            offsiteSupportApplied: null,
            medicalCoordinationType: MedicalCoordinationType.Unspecified,
            trialUseSupportType: TrialUseSupportType.Unspecified,
            regionalCollaborationApplied: null,
            intensiveSupportApplied: null,
            emergencyAdmissionApplied: null,
            recipientConfirmation: RecipientConfirmationStatus.Unspecified,
            actor, ct);

    public async Task<DailyRecordDto> ExecuteAsync(
        Guid recipientId, DateOnly serviceDate,
        Attendance attendance, TransportKind transport, bool mealProvided, string? note,
        TimeOnly? serviceStartTime,
        TimeOnly? serviceEndTime,
        int? specialVisitSupportMinutes,
        bool? offsiteSupportApplied,
        MedicalCoordinationType medicalCoordinationType,
        TrialUseSupportType trialUseSupportType,
        bool? regionalCollaborationApplied,
        bool? intensiveSupportApplied,
        bool? emergencyAdmissionApplied,
        RecipientConfirmationStatus recipientConfirmation,
        string actor, CancellationToken ct)
    {
        if (recipientId == Guid.Empty)
            throw new ArgumentException("利用者IDが指定されていません。", nameof(recipientId));
        DateValidator.EnsureValid(serviceDate, nameof(serviceDate));

        var existing = await repo.ListByRecipientAndDateAsync(recipientId, serviceDate, ct);
        if (existing.Any(r => r.Kind == Domain.Enums.RecordKind.New))
            throw new InvalidOperationException("同一日に新規記録が既に存在します。訂正または取消を使用してください。");

        var entity = Domain.Entities.DailyRecord.NewRecord(
            Guid.NewGuid(), recipientId, serviceDate,
            attendance, transport, mealProvided, note,
            actor, clock.GetUtcNow(),
            serviceStartTime, serviceEndTime, specialVisitSupportMinutes,
            offsiteSupportApplied, medicalCoordinationType, trialUseSupportType,
            regionalCollaborationApplied, intensiveSupportApplied,
            emergencyAdmissionApplied, recipientConfirmation);
        await repo.AddAsync(entity, ct);
        await uow.SaveChangesAsync(ct);
        return Map(entity);
    }

    internal static DailyRecordDto Map(Domain.Entities.DailyRecord e) =>
        new(e.Id, e.RecipientId, e.ServiceDate, e.Kind, e.OriginId,
            e.Attendance, e.Transport, e.MealProvided, e.Note)
        {
            ServiceStartTime = e.ServiceStartTime,
            ServiceEndTime = e.ServiceEndTime,
            SpecialVisitSupportMinutes = e.SpecialVisitSupportMinutes,
            OffsiteSupportApplied = e.OffsiteSupportApplied,
            MedicalCoordinationType = e.MedicalCoordinationType,
            TrialUseSupportType = e.TrialUseSupportType,
            RegionalCollaborationApplied = e.RegionalCollaborationApplied,
            IntensiveSupportApplied = e.IntensiveSupportApplied,
            EmergencyAdmissionApplied = e.EmergencyAdmissionApplied,
            RecipientConfirmation = e.RecipientConfirmation,
        };
}
