using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.UseCases.Office;
using Tsumugi.Application.UseCases.Recipient;
using Tsumugi.Application.UseCases.Wage;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.App.ViewModels;

/// <summary>
/// 利用者別時給期間の一覧・追記 ViewModel。
/// 事業所 ＋ 利用者を選択すると既存レートを表示し、新しい期間を追記できる。
/// </summary>
public sealed partial class RecipientHourlyRateViewModel(
    SetRecipientHourlyRateUseCase setRate,
    QueryRecipientHourlyRateUseCase queryRates,
    ListOfficesUseCase listOffices,
    ListRecipientsUseCase listRecipients) : ViewModelBase
{
    [ObservableProperty] private OfficeDto? _selectedOffice;
    [ObservableProperty] private RecipientDto? _selectedRecipient;
    [ObservableProperty] private DateOnly _periodStart = new(DateTime.UtcNow.Year, 4, 1);
    [ObservableProperty] private DateOnly? _periodEnd;
    [ObservableProperty] private int _hourlyYen;
    [ObservableProperty] private string? _errorMessage;

    public ObservableCollection<OfficeDto> Offices { get; } = new();
    public ObservableCollection<RecipientDto> Recipients { get; } = new();
    public ObservableCollection<RecipientHourlyRateRowViewModel> Rates { get; } = new();

    partial void OnSelectedOfficeChanged(OfficeDto? value)
    {
        SaveCommand.NotifyCanExecuteChanged();
        if (SelectedOffice is not null && SelectedRecipient is not null)
            _ = SafeRefreshRatesAsync();
    }

    partial void OnSelectedRecipientChanged(RecipientDto? value)
    {
        SaveCommand.NotifyCanExecuteChanged();
        if (SelectedOffice is not null && SelectedRecipient is not null)
            _ = SafeRefreshRatesAsync();
    }

    private async Task SafeRefreshRatesAsync()
    {
        try { await RefreshRatesAsync(); }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    private bool CanSave() => SelectedOffice is not null && SelectedRecipient is not null;

    /// <summary>View の Loaded から呼ばれる初期化フック。事業所・利用者一覧を読み込む。</summary>
    public Task InitializeAsync(CancellationToken ct = default) => LoadAsync(ct);

    [RelayCommand]
    public async Task LoadAsync(CancellationToken ct = default)
    {
        var offices = await listOffices.ExecuteAsync(ct);
        Offices.Clear();
        foreach (var o in offices) Offices.Add(o);

        var recipients = await listRecipients.ExecuteAsync(includeArchived: false, ct);
        Recipients.Clear();
        foreach (var r in recipients) Recipients.Add(r);
    }

    [RelayCommand]
    public async Task RefreshRatesAsync(CancellationToken ct = default)
    {
        if (SelectedOffice is null || SelectedRecipient is null) return;

        var list = await queryRates.ExecuteAsync(SelectedOffice.Id, SelectedRecipient.Id, ct);
        Rates.Clear();
        foreach (var dto in list) Rates.Add(new RecipientHourlyRateRowViewModel(dto));
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    public async Task SaveAsync(CancellationToken ct = default)
    {
        if (SelectedOffice is null || SelectedRecipient is null) return;

        try
        {
            await setRate.ExecuteAsync(
                SelectedOffice.Id,
                SelectedRecipient.Id,
                new DateRange(PeriodStart, PeriodEnd),
                HourlyYen,
                actor: Environment.UserName,
                ct);
            ErrorMessage = null;
            await RefreshRatesAsync(ct);
        }
        // 保存失敗（DB 制約違反等の想定外を含む）は UI に必ず表示する
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }
}
