using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.UseCases;
using Tsumugi.Application.UseCases.Claim;
using Tsumugi.Application.UseCases.DailyRecord;
using Tsumugi.Application.UseCases.Office;
using Tsumugi.Application.UseCases.Recipient;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.App.ViewModels;

public sealed partial class DailyRecordViewModel(
    RecordDailyRecordUseCase record,
    CorrectDailyRecordUseCase correct,
    CancelDailyRecordUseCase cancel,
    QueryMonthDailyRecordsUseCase query,
    ListRecipientsUseCase listRecipients,
    ListOfficesUseCase listOffices,
    QueryIntensiveSupportEpisodeUseCase queryEpisode,
    SetIntensiveSupportEpisodeUseCase setEpisode) : ViewModelBase
{
    [ObservableProperty] private Guid _recipientId;
    [ObservableProperty] private RecipientDto? _selectedRecipient;
    [ObservableProperty] private int _year;
    [ObservableProperty] private int _month;
    [ObservableProperty] private DailyCellViewModel? _selectedCell;

    [ObservableProperty] private Attendance _editorAttendance = Attendance.Present;
    [ObservableProperty] private TransportKind _editorTransport;
    [ObservableProperty] private bool _editorMealProvided;
    [ObservableProperty] private string _editorNote = string.Empty;
    [ObservableProperty] private TimeOnly? _editorServiceStartTime;
    [ObservableProperty] private TimeOnly? _editorServiceEndTime;
    [ObservableProperty] private int? _editorSpecialVisitSupportMinutes;
    [ObservableProperty] private bool? _editorOffsiteSupportApplied;
    [ObservableProperty] private MedicalCoordinationType _editorMedicalCoordinationType;
    [ObservableProperty] private TrialUseSupportType _editorTrialUseSupportType;
    [ObservableProperty] private bool? _editorRegionalCollaborationApplied;
    [ObservableProperty] private bool? _editorIntensiveSupportApplied;
    [ObservableProperty] private bool? _editorEmergencyAdmissionApplied;
    [ObservableProperty] private RecipientConfirmationStatus _editorRecipientConfirmation;
    [ObservableProperty] private string? _dailyRecordErrorMessage;

    [ObservableProperty] private OfficeDto? _selectedOffice;
    [ObservableProperty] private Guid _officeId;
    [ObservableProperty] private DateOnly? _episodeStartDate;
    [ObservableProperty] private Guid? _episodeCurrentHeadId;
    [ObservableProperty] private Guid? _episodeEffectiveHeadId;
    [ObservableProperty] private string? _episodeErrorMessage;
    [ObservableProperty] private bool _episodeHistoryLoaded;
    private Guid _loadedEpisodeOfficeId;
    private Guid _loadedEpisodeRecipientId;

    public ObservableCollection<DailyCellViewModel> Cells { get; } = new();
    public ObservableCollection<RecipientDto> Recipients { get; } = new();
    public ObservableCollection<OfficeDto> Offices { get; } = new();
    public ObservableCollection<IntensiveSupportEpisodeQueryRevisionDto> EpisodeRevisions { get; } = new();

    public IReadOnlyList<Attendance> AttendanceOptions { get; } = Enum.GetValues<Attendance>();
    public IReadOnlyList<TransportKind> TransportOptions { get; } = Enum.GetValues<TransportKind>();
    public IReadOnlyList<MedicalCoordinationType> MedicalCoordinationOptions { get; } =
        Enum.GetValues<MedicalCoordinationType>();
    public IReadOnlyList<TrialUseSupportType> TrialUseSupportOptions { get; } =
        Enum.GetValues<TrialUseSupportType>();
    public IReadOnlyList<RecipientConfirmationStatus> RecipientConfirmationOptions { get; } =
        Enum.GetValues<RecipientConfirmationStatus>();

    // 後方互換 API。XAML からは ObservableProperty / SelectedRecipient を直接バインドする。
    public void SetRecipient(Guid id) => RecipientId = id;
    public void SetMonth(int year, int month) { Year = year; Month = month; }

    /// <summary>ナビゲーション由来の事業所・利用者・対象日を読み込み、編集対象を選択する。</summary>
    public async Task<bool> ApplyNavigationContextAsync(
        Guid? officeId,
        Guid? recipientId,
        DateOnly? serviceDate,
        ServiceMonth? serviceMonth,
        CancellationToken ct = default)
    {
        var baseline = (OfficeId, RecipientId, Year, Month);
        if (officeId is not null)
            await LoadOfficesAsync(ct);
        if (recipientId is not null)
            await LoadRecipientsAsync(ct);
        if (baseline != (OfficeId, RecipientId, Year, Month))
            return false;

        if (officeId is { } targetOfficeId)
        {
            SelectedOffice = Offices.SingleOrDefault(x => x.Id == targetOfficeId);
            if (SelectedOffice is null)
                return false;
        }

        if (recipientId is { } id)
        {
            SelectedRecipient = Recipients.SingleOrDefault(x => x.Id == id);
            if (SelectedRecipient is null)
                return false;
        }

        if (serviceDate is { } date)
            SetMonth(date.Year, date.Month);
        else if (serviceMonth is { } month)
            SetMonth(month.Year, month.Month);

        if (serviceDate is null && serviceMonth is null)
            return true;
        if (recipientId is null)
            return false;

        var loaded = await LoadDailyContextAsync(serviceDate, ct);
        return loaded
            && (officeId is null || OfficeId == officeId)
            && (serviceDate is null || SelectedCell?.Date == serviceDate);
    }

    /// <summary>View の Loaded から呼ばれる初期化フック。利用者一覧を読み込む。</summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await LoadRecipientsAsync(ct);
        await LoadOfficesAsync(ct);
    }

    public async Task LoadRecipientsAsync(CancellationToken ct = default)
    {
        var list = await listRecipients.ExecuteAsync(includeArchived: false, ct);
        Recipients.Clear();
        foreach (var r in list) Recipients.Add(r);
    }

    public async Task LoadOfficesAsync(CancellationToken ct = default)
    {
        var list = await listOffices.ExecuteAsync(ct);
        Offices.Clear();
        foreach (var office in list) Offices.Add(office);
    }

    partial void OnSelectedRecipientChanged(RecipientDto? value)
        => RecipientId = value?.Id ?? Guid.Empty;

    partial void OnSelectedOfficeChanged(OfficeDto? value)
        => OfficeId = value?.Id ?? Guid.Empty;

    partial void OnOfficeIdChanged(Guid value) => ClearEpisodeContext();

    partial void OnSelectedCellChanged(DailyCellViewModel? value)
    {
        if (value is null)
        {
            ResetDailyEditor();
            return;
        }
        EditorAttendance = value?.EffectiveAttendance ?? Attendance.Present;
        EditorTransport = value?.EffectiveTransport ?? TransportKind.None;
        EditorMealProvided = value?.EffectiveMealProvided ?? false;
        EditorNote = value?.EffectiveNote ?? string.Empty;
        EditorServiceStartTime = value?.EffectiveServiceStartTime;
        EditorServiceEndTime = value?.EffectiveServiceEndTime;
        EditorSpecialVisitSupportMinutes = value?.EffectiveSpecialVisitSupportMinutes;
        EditorOffsiteSupportApplied = value?.EffectiveOffsiteSupportApplied;
        EditorMedicalCoordinationType = value?.EffectiveMedicalCoordinationType
            ?? MedicalCoordinationType.Unspecified;
        EditorTrialUseSupportType = value?.EffectiveTrialUseSupportType
            ?? TrialUseSupportType.Unspecified;
        EditorRegionalCollaborationApplied = value?.EffectiveRegionalCollaborationApplied;
        EditorIntensiveSupportApplied = value?.EffectiveIntensiveSupportApplied;
        EditorEmergencyAdmissionApplied = value?.EffectiveEmergencyAdmissionApplied;
        EditorRecipientConfirmation = value?.EffectiveRecipientConfirmation
            ?? RecipientConfirmationStatus.Unspecified;
    }

    // ObservableProperty 変更時に CanExecute を再評価して F5 / ボタンの活性を切り替える。
    partial void OnRecipientIdChanged(Guid value)
    {
        ClearDailyContext();
        ClearEpisodeContext();
        LoadCommand.NotifyCanExecuteChanged();
    }

    partial void OnYearChanged(int value)
    {
        ClearDailyContext();
        LoadCommand.NotifyCanExecuteChanged();
    }

    partial void OnMonthChanged(int value)
    {
        ClearDailyContext();
        LoadCommand.NotifyCanExecuteChanged();
    }

    // CanLoad は ConcurrencyToken ではなく入力の妥当性のみを判定する純粋関数。
    private bool CanLoad() =>
        RecipientId != Guid.Empty
        && Year is >= 1900 and <= 9999
        && Month is >= 1 and <= 12;

    [RelayCommand(CanExecute = nameof(CanLoad))]
    public async Task LoadAsync() => await LoadDailyContextAsync(SelectedCell?.Date, default);

    private async Task<bool> LoadDailyContextAsync(
        DateOnly? selectedDate,
        CancellationToken ct)
    {
        // CanExecute で防げない経路（直接呼び出し）でも DateTime.DaysInMonth(0,0) に落ちないこと。
        if (!CanLoad()) { ClearDailyContext(); return false; }

        var recipientId = RecipientId;
        var year = Year;
        var month = Month;
        Cells.Clear();
        SelectedCell = null;
        var effective = await query.ExecuteAsync(recipientId, year, month, ct);
        if (!MatchesDailyContext(recipientId, year, month)) return false;

        var daysInMonth = DateTime.DaysInMonth(year, month);
        for (var d = 1; d <= daysInMonth; d++)
        {
            var date = new DateOnly(year, month, d);
            var cell = new DailyCellViewModel(
                recipientId, date, record, correct, cancel, LoadAsync);
            if (effective.TryGetValue(date, out var dto))
            {
                cell.EffectiveId = dto.Id;
                cell.EffectiveAttendance = dto.Attendance;
                cell.EffectiveTransport = dto.Transport;
                cell.EffectiveMealProvided = dto.MealProvided;
                cell.EffectiveNote = dto.Note;
                cell.EffectiveServiceStartTime = dto.ServiceStartTime;
                cell.EffectiveServiceEndTime = dto.ServiceEndTime;
                cell.EffectiveSpecialVisitSupportMinutes = dto.SpecialVisitSupportMinutes;
                cell.EffectiveOffsiteSupportApplied = dto.OffsiteSupportApplied;
                cell.EffectiveMedicalCoordinationType = dto.MedicalCoordinationType;
                cell.EffectiveTrialUseSupportType = dto.TrialUseSupportType;
                cell.EffectiveRegionalCollaborationApplied = dto.RegionalCollaborationApplied;
                cell.EffectiveIntensiveSupportApplied = dto.IntensiveSupportApplied;
                cell.EffectiveEmergencyAdmissionApplied = dto.EmergencyAdmissionApplied;
                cell.EffectiveRecipientConfirmation = dto.RecipientConfirmation;
            }
            Cells.Add(cell);
        }
        SelectedCell = selectedDate is { } preservedDate
            ? Cells.SingleOrDefault(cell => cell.Date == preservedDate)
            : null;
        return selectedDate is null || SelectedCell is not null;
    }

    [RelayCommand]
    private async Task SaveSelectedDailyRecordAsync()
    {
        if (SelectedCell is not { } cell)
        {
            DailyRecordErrorMessage = "編集する日を選択してください。";
            return;
        }
        if (cell.RecipientId != RecipientId
            || cell.Date.Year != Year
            || cell.Date.Month != Month)
        {
            DailyRecordErrorMessage =
                "表示条件が変更されています。最新状態を再読込してください。";
            return;
        }

        try
        {
            if (cell.EffectiveId is { } effectiveId)
            {
                await correct.ExecuteAsync(
                    effectiveId, EditorAttendance, EditorTransport, EditorMealProvided,
                    NullIfEmpty(EditorNote), EditorServiceStartTime, EditorServiceEndTime,
                    EditorSpecialVisitSupportMinutes, EditorOffsiteSupportApplied,
                    EditorMedicalCoordinationType, EditorTrialUseSupportType,
                    EditorRegionalCollaborationApplied, EditorIntensiveSupportApplied,
                    EditorEmergencyAdmissionApplied, EditorRecipientConfirmation,
                    Environment.UserName, default);
            }
            else
            {
                await record.ExecuteAsync(
                    RecipientId, cell.Date, EditorAttendance, EditorTransport, EditorMealProvided,
                    NullIfEmpty(EditorNote), EditorServiceStartTime, EditorServiceEndTime,
                    EditorSpecialVisitSupportMinutes, EditorOffsiteSupportApplied,
                    EditorMedicalCoordinationType, EditorTrialUseSupportType,
                    EditorRegionalCollaborationApplied, EditorIntensiveSupportApplied,
                    EditorEmergencyAdmissionApplied, EditorRecipientConfirmation,
                    Environment.UserName, default);
            }
            DailyRecordErrorMessage = null;
            await LoadAsync();
        }
        catch (ArgumentException ex)
        {
            DailyRecordErrorMessage = ex.Message;
        }
        catch (InvalidOperationException)
        {
            DailyRecordErrorMessage = "日次記録は既に更新されています。最新状態を再読込してください。";
            await LoadAsync();
        }
    }

    [RelayCommand]
    public async Task LoadEpisodeAsync()
    {
        var officeId = OfficeId;
        var recipientId = RecipientId;
        ClearEpisodeContext();
        if (officeId == Guid.Empty || recipientId == Guid.Empty) return;

        var history = await queryEpisode.ExecuteAsync(
            new QueryIntensiveSupportEpisodeRequest(officeId, recipientId), default);
        if (!MatchesEpisodeContext(officeId, recipientId)) return;

        foreach (var revision in history.Revisions) EpisodeRevisions.Add(revision);
        EpisodeCurrentHeadId = history.CurrentHeadId;
        EpisodeEffectiveHeadId = history.EffectiveHeadId;
        EpisodeStartDate = history.Revisions.Count == 0
            ? null
            : history.Revisions[^1].StartDate;
        _loadedEpisodeOfficeId = officeId;
        _loadedEpisodeRecipientId = recipientId;
        EpisodeHistoryLoaded = true;
        EpisodeErrorMessage = null;
    }

    [RelayCommand]
    private async Task SaveEpisodeAsync()
    {
        if (!CanEditEpisode())
        {
            EpisodeErrorMessage = "事業所と利用者を選択してください。";
            return;
        }
        if (!HasLoadedEpisodeContext())
        {
            EpisodeErrorMessage = "重度支援対象期間の履歴を再読込してください。";
            return;
        }

        var kind = EpisodeCurrentHeadId is null ? RecordKind.New : RecordKind.Correct;
        await ExecuteEpisodeSaveAsync(kind, EpisodeStartDate);
    }

    [RelayCommand]
    private async Task CancelEpisodeAsync()
    {
        if (!CanEditEpisode())
        {
            EpisodeErrorMessage = "事業所と利用者を選択してください。";
            return;
        }
        if (!HasLoadedEpisodeContext())
        {
            EpisodeErrorMessage = "重度支援対象期間の履歴を再読込してください。";
            return;
        }
        if (EpisodeCurrentHeadId is null)
        {
            EpisodeErrorMessage = "取消対象の重度支援対象期間がありません。";
            return;
        }
        await ExecuteEpisodeSaveAsync(RecordKind.Cancel, startDate: null);
    }

    private async Task ExecuteEpisodeSaveAsync(RecordKind kind, DateOnly? startDate)
    {
        try
        {
            await setEpisode.ExecuteAsync(
                new SetIntensiveSupportEpisodeRequest(
                    OfficeId, RecipientId, kind, EpisodeCurrentHeadId, startDate),
                Environment.UserName, default);
            EpisodeErrorMessage = null;
            await LoadEpisodeAsync();
        }
        catch (ClaimInputSaveException ex)
        {
            var message = ex.Code == ClaimInputSaveErrorCode.ExpectedHeadMismatch
                ? "重度支援対象期間は既に更新されています。最新状態を再読込してください。"
                : "重度支援対象期間の入力内容を確認してください。";
            await LoadEpisodeAsync();
            EpisodeErrorMessage = message;
        }
    }

    private bool CanEditEpisode() => OfficeId != Guid.Empty && RecipientId != Guid.Empty;

    private bool MatchesEpisodeContext(Guid officeId, Guid recipientId) =>
        OfficeId == officeId && RecipientId == recipientId;

    private bool HasLoadedEpisodeContext() =>
        EpisodeHistoryLoaded
        && _loadedEpisodeOfficeId == OfficeId
        && _loadedEpisodeRecipientId == RecipientId;

    private bool MatchesDailyContext(Guid recipientId, int year, int month) =>
        RecipientId == recipientId && Year == year && Month == month;

    private void ClearDailyContext()
    {
        Cells.Clear();
        SelectedCell = null;
        ResetDailyEditor();
        DailyRecordErrorMessage = null;
    }

    private void ResetDailyEditor()
    {
        EditorAttendance = Attendance.Present;
        EditorTransport = TransportKind.None;
        EditorMealProvided = false;
        EditorNote = string.Empty;
        EditorServiceStartTime = null;
        EditorServiceEndTime = null;
        EditorSpecialVisitSupportMinutes = null;
        EditorOffsiteSupportApplied = null;
        EditorMedicalCoordinationType = MedicalCoordinationType.Unspecified;
        EditorTrialUseSupportType = TrialUseSupportType.Unspecified;
        EditorRegionalCollaborationApplied = null;
        EditorIntensiveSupportApplied = null;
        EditorEmergencyAdmissionApplied = null;
        EditorRecipientConfirmation = RecipientConfirmationStatus.Unspecified;
    }

    private void ClearEpisodeContext()
    {
        EpisodeRevisions.Clear();
        EpisodeCurrentHeadId = null;
        EpisodeEffectiveHeadId = null;
        EpisodeStartDate = null;
        EpisodeErrorMessage = null;
        EpisodeHistoryLoaded = false;
        _loadedEpisodeOfficeId = Guid.Empty;
        _loadedEpisodeRecipientId = Guid.Empty;
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
