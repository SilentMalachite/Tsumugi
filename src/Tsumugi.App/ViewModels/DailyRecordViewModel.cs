using System.Collections.ObjectModel;
using Tsumugi.Application.UseCases.DailyRecord;

namespace Tsumugi.App.ViewModels;

public sealed partial class DailyRecordViewModel(
    RecordDailyRecordUseCase record,
    CorrectDailyRecordUseCase correct,
    CancelDailyRecordUseCase cancel,
    QueryMonthDailyRecordsUseCase query) : ViewModelBase
{
    private Guid _recipientId;
    private int _year;
    private int _month;

    public ObservableCollection<DailyCellViewModel> Cells { get; } = new();

    public void SetRecipient(Guid id) => _recipientId = id;
    public void SetMonth(int year, int month) { _year = year; _month = month; }

    public async Task LoadAsync()
    {
        Cells.Clear();
        var daysInMonth = DateTime.DaysInMonth(_year, _month);
        var effective = await query.ExecuteAsync(_recipientId, _year, _month, default);
        for (var d = 1; d <= daysInMonth; d++)
        {
            var date = new DateOnly(_year, _month, d);
            var cell = new DailyCellViewModel(_recipientId, date, record, correct, cancel, LoadAsync);
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
