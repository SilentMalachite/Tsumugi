using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tsumugi.Application.UseCases.WorkRecord;

namespace Tsumugi.App.ViewModels;

/// <summary>作業実績グリッドの 1 セル。WorkedMinutes の入力/訂正/取消を担う。</summary>
public sealed partial class WorkCellViewModel(
    Guid recipientId,
    DateOnly date,
    RecordWorkUseCase record,
    CorrectWorkUseCase correct,
    CancelWorkUseCase cancel,
    Func<Task> reload) : ViewModelBase
{
    public DateOnly Date { get; } = date;

    [ObservableProperty] private Guid? _effectiveId;
    [ObservableProperty] private int? _workedMinutes;

    /// <summary>WorkedMinutes を保存する統一コマンド。EffectiveId 有無で Record/Correct を分岐。</summary>
    [RelayCommand]
    private async Task SaveWorkedMinutesAsync(int? minutes)
    {
        if (EffectiveId is null)
        {
            await record.ExecuteAsync(recipientId, Date,
                workedMinutes: minutes, pieceCount: null, pieceUnitYen: null, points: null,
                note: null, actor: Environment.UserName, default);
        }
        else
        {
            await correct.ExecuteAsync(EffectiveId.Value,
                workedMinutes: minutes, pieceCount: null, pieceUnitYen: null, points: null,
                note: null, actor: Environment.UserName, default);
        }
        await reload();
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        if (EffectiveId is null) return;
        await cancel.ExecuteAsync(EffectiveId.Value, actor: Environment.UserName, default);
        await reload();
    }
}
