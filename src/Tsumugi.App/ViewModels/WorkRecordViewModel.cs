using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.UseCases.Recipient;
using Tsumugi.Application.UseCases.WorkRecord;

namespace Tsumugi.App.ViewModels;

/// <summary>
/// 作業実績の月グリッド ViewModel。1 利用者 × 当月日数。
/// DailyRecordViewModel と同パターン。WorkedMinutes 編集のみをサポート（最小実装）。
/// Piece/Points 編集は将来の拡張で別 ViewModel/カラムへ。
/// </summary>
public sealed partial class WorkRecordViewModel(
    RecordWorkUseCase record,
    CorrectWorkUseCase correct,
    CancelWorkUseCase cancel,
    QueryMonthWorkUseCase query,
    ListRecipientsUseCase listRecipients) : ViewModelBase
{
    [ObservableProperty] private Guid _recipientId;
    [ObservableProperty] private RecipientDto? _selectedRecipient;
    [ObservableProperty] private int _year;
    [ObservableProperty] private int _month;

    public ObservableCollection<WorkCellViewModel> Cells { get; } = new();
    public ObservableCollection<RecipientDto> Recipients { get; } = new();

    public void SetRecipient(Guid id) => RecipientId = id;
    public void SetMonth(int year, int month) { Year = year; Month = month; }

    public Task InitializeAsync(CancellationToken ct = default) => LoadRecipientsAsync(ct);

    public async Task LoadRecipientsAsync(CancellationToken ct = default)
    {
        var list = await listRecipients.ExecuteAsync(includeArchived: false, ct);
        Recipients.Clear();
        foreach (var r in list) Recipients.Add(r);
    }

    partial void OnSelectedRecipientChanged(RecipientDto? value)
        => RecipientId = value?.Id ?? Guid.Empty;

    partial void OnRecipientIdChanged(Guid value) => LoadCommand.NotifyCanExecuteChanged();
    partial void OnYearChanged(int value) => LoadCommand.NotifyCanExecuteChanged();
    partial void OnMonthChanged(int value) => LoadCommand.NotifyCanExecuteChanged();

    private bool CanLoad() =>
        RecipientId != Guid.Empty
        && Year is >= 1900 and <= 9999
        && Month is >= 1 and <= 12;

    [RelayCommand(CanExecute = nameof(CanLoad))]
    public async Task LoadAsync()
    {
        if (!CanLoad()) { Cells.Clear(); return; }

        Cells.Clear();
        var daysInMonth = DateTime.DaysInMonth(Year, Month);
        var effective = await query.ExecuteAsync(RecipientId, Year, Month, default);
        for (var d = 1; d <= daysInMonth; d++)
        {
            var date = new DateOnly(Year, Month, d);
            var cell = new WorkCellViewModel(RecipientId, date, record, correct, cancel, LoadAsync);
            if (effective.TryGetValue(date, out var dto))
            {
                cell.EffectiveId = dto.Id;
                cell.WorkedMinutes = dto.WorkedMinutes;
            }
            Cells.Add(cell);
        }
    }
}
