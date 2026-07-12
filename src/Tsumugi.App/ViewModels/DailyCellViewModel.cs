using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tsumugi.Application.UseCases.DailyRecord;
using Tsumugi.Domain.Enums;

namespace Tsumugi.App.ViewModels;

public sealed partial class DailyCellViewModel(
    Guid recipientId,
    DateOnly date,
    RecordDailyRecordUseCase record,
    CorrectDailyRecordUseCase correct,
    CancelDailyRecordUseCase cancel,
    Func<Task> reload) : ViewModelBase
{
    public DateOnly Date { get; } = date;

    [ObservableProperty] private Guid? _effectiveId;
    [ObservableProperty] private Attendance? _effectiveAttendance;
    [ObservableProperty] private TransportKind _effectiveTransport;
    [ObservableProperty] private bool _effectiveMealProvided;
    [ObservableProperty] private string? _effectiveNote;
    [ObservableProperty] private TimeOnly? _effectiveServiceStartTime;
    [ObservableProperty] private TimeOnly? _effectiveServiceEndTime;
    [ObservableProperty] private int? _effectiveSpecialVisitSupportMinutes;
    [ObservableProperty] private bool? _effectiveOffsiteSupportApplied;
    [ObservableProperty] private MedicalCoordinationType _effectiveMedicalCoordinationType;
    [ObservableProperty] private TrialUseSupportType _effectiveTrialUseSupportType;
    [ObservableProperty] private bool? _effectiveRegionalCollaborationApplied;
    [ObservableProperty] private bool? _effectiveIntensiveSupportApplied;
    [ObservableProperty] private bool? _effectiveEmergencyAdmissionApplied;
    [ObservableProperty] private RecipientConfirmationStatus _effectiveRecipientConfirmation;

    /// <summary>
    /// 出欠を変更する統一コマンド。既存記録があれば訂正、無ければ新規登録に分岐する。
    /// UI は本コマンドだけを叩けば良いので、Record/Correct の分岐ミスで例外を踏まない。
    /// </summary>
    [RelayCommand]
    private async Task SetAttendanceAsync(Attendance attendance)
    {
        if (EffectiveId is null)
        {
            await record.ExecuteAsync(recipientId, Date,
                attendance, TransportKind.None, mealProvided: false, note: null,
                serviceStartTime: null, serviceEndTime: null,
                specialVisitSupportMinutes: null, offsiteSupportApplied: null,
                MedicalCoordinationType.Unspecified, TrialUseSupportType.Unspecified,
                regionalCollaborationApplied: null, intensiveSupportApplied: null,
                emergencyAdmissionApplied: null, RecipientConfirmationStatus.Unspecified,
                actor: Environment.UserName, default);
        }
        else
        {
            await correct.ExecuteAsync(EffectiveId.Value,
                attendance, EffectiveTransport, EffectiveMealProvided, EffectiveNote,
                EffectiveServiceStartTime, EffectiveServiceEndTime,
                EffectiveSpecialVisitSupportMinutes, EffectiveOffsiteSupportApplied,
                EffectiveMedicalCoordinationType, EffectiveTrialUseSupportType,
                EffectiveRegionalCollaborationApplied, EffectiveIntensiveSupportApplied,
                EffectiveEmergencyAdmissionApplied, EffectiveRecipientConfirmation,
                actor: Environment.UserName, default);
        }
        await reload();
    }

    [RelayCommand]
    private async Task RecordAsync(Attendance attendance)
    {
        await record.ExecuteAsync(recipientId, Date,
            attendance, TransportKind.None, mealProvided: false, note: null,
            serviceStartTime: null, serviceEndTime: null,
            specialVisitSupportMinutes: null, offsiteSupportApplied: null,
            MedicalCoordinationType.Unspecified, TrialUseSupportType.Unspecified,
            regionalCollaborationApplied: null, intensiveSupportApplied: null,
            emergencyAdmissionApplied: null, RecipientConfirmationStatus.Unspecified,
            actor: Environment.UserName, default);
        await reload();
    }

    [RelayCommand]
    private async Task CorrectAsync(Attendance attendance)
    {
        if (EffectiveId is null) return;
        await correct.ExecuteAsync(EffectiveId.Value,
            attendance, EffectiveTransport, EffectiveMealProvided, EffectiveNote,
            EffectiveServiceStartTime, EffectiveServiceEndTime,
            EffectiveSpecialVisitSupportMinutes, EffectiveOffsiteSupportApplied,
            EffectiveMedicalCoordinationType, EffectiveTrialUseSupportType,
            EffectiveRegionalCollaborationApplied, EffectiveIntensiveSupportApplied,
            EffectiveEmergencyAdmissionApplied, EffectiveRecipientConfirmation,
            actor: Environment.UserName, default);
        await reload();
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        if (EffectiveId is null) return;
        await cancel.ExecuteAsync(EffectiveId.Value, Environment.UserName, default);
        await reload();
    }
}
