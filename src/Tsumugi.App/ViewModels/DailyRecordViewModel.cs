using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.UseCases.DailyRecord;
using Tsumugi.Application.UseCases.Recipient;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.App.ViewModels;

public sealed partial class DailyRecordViewModel(
    RecordDailyRecordUseCase record,
    CorrectDailyRecordUseCase correct,
    CancelDailyRecordUseCase cancel,
    QueryMonthDailyRecordsUseCase query,
    ListRecipientsUseCase listRecipients) : ViewModelBase
{
    [ObservableProperty] private Guid _recipientId;
    [ObservableProperty] private RecipientDto? _selectedRecipient;
    [ObservableProperty] private int _year;
    [ObservableProperty] private int _month;

    public ObservableCollection<DailyCellViewModel> Cells { get; } = new();
    public ObservableCollection<RecipientDto> Recipients { get; } = new();

    // 後方互換 API。XAML からは ObservableProperty / SelectedRecipient を直接バインドする。
    public void SetRecipient(Guid id) => RecipientId = id;
    public void SetMonth(int year, int month) { Year = year; Month = month; }

    /// <summary>ナビゲーション由来の利用者・サービス年月だけを適用する。</summary>
    public async Task<bool> ApplyNavigationContextAsync(
        Guid? recipientId,
        DateOnly? serviceDate,
        ServiceMonth? serviceMonth,
        CancellationToken ct = default)
    {
        if (recipientId is { } id)
        {
            await LoadRecipientsAsync(ct);
            SelectedRecipient = Recipients.SingleOrDefault(x => x.Id == id);
            if (SelectedRecipient is null)
                return false;
        }

        if (serviceDate is { } date)
            SetMonth(date.Year, date.Month);
        else if (serviceMonth is { } month)
            SetMonth(month.Year, month.Month);

        return true;
    }

    /// <summary>View の Loaded から呼ばれる初期化フック。利用者一覧を読み込む。</summary>
    public Task InitializeAsync(CancellationToken ct = default) => LoadRecipientsAsync(ct);

    public async Task LoadRecipientsAsync(CancellationToken ct = default)
    {
        var list = await listRecipients.ExecuteAsync(includeArchived: false, ct);
        Recipients.Clear();
        foreach (var r in list) Recipients.Add(r);
    }

    partial void OnSelectedRecipientChanged(RecipientDto? value)
        => RecipientId = value?.Id ?? Guid.Empty;

    // ObservableProperty 変更時に CanExecute を再評価して F5 / ボタンの活性を切り替える。
    partial void OnRecipientIdChanged(Guid value) => LoadCommand.NotifyCanExecuteChanged();
    partial void OnYearChanged(int value) => LoadCommand.NotifyCanExecuteChanged();
    partial void OnMonthChanged(int value) => LoadCommand.NotifyCanExecuteChanged();

    // CanLoad は ConcurrencyToken ではなく入力の妥当性のみを判定する純粋関数。
    private bool CanLoad() =>
        RecipientId != Guid.Empty
        && Year is >= 1900 and <= 9999
        && Month is >= 1 and <= 12;

    [RelayCommand(CanExecute = nameof(CanLoad))]
    public async Task LoadAsync()
    {
        // CanExecute で防げない経路（直接呼び出し）でも DateTime.DaysInMonth(0,0) に落ちないこと。
        if (!CanLoad()) { Cells.Clear(); return; }

        Cells.Clear();
        var daysInMonth = DateTime.DaysInMonth(Year, Month);
        var effective = await query.ExecuteAsync(RecipientId, Year, Month, default);
        for (var d = 1; d <= daysInMonth; d++)
        {
            var date = new DateOnly(Year, Month, d);
            var cell = new DailyCellViewModel(RecipientId, date, record, correct, cancel, LoadAsync);
            if (effective.TryGetValue(date, out var dto))
            {
                cell.EffectiveId = dto.Id;
                cell.EffectiveAttendance = dto.Attendance;
                cell.EffectiveTransport = dto.Transport;
                cell.EffectiveMealProvided = dto.MealProvided;
            }
            Cells.Add(cell);
        }
    }
}
