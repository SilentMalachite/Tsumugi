using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tsumugi.App.Formatting;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.UseCases.Office;
using Tsumugi.Application.UseCases.Wage;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.App.ViewModels;

/// <summary>
/// 工賃原資（月次）と工賃設定（期間マスタ）の編集 ViewModel。
/// D2 の SetWageFundUseCase + ConfigureWageSettingsUseCase を配線。
/// </summary>
public sealed partial class WageFundSettingsViewModel(
    SetWageFundUseCase setFund,
    ConfigureWageSettingsUseCase configureSettings,
    ListOfficesUseCase listOfficesUseCase) : ViewModelBase
{
    [ObservableProperty] private OfficeDto? _selectedOffice;
    [ObservableProperty] private Guid _officeId;
    [ObservableProperty] private int _year = DateTime.UtcNow.Year;
    [ObservableProperty] private int _month = DateTime.UtcNow.Month;
    [ObservableProperty] private int _totalYen;
    [ObservableProperty] private string? _fundNote;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _formattedTotalYen;

    // Settings 編集用
    [ObservableProperty] private DateOnly _periodStart = new(DateTime.UtcNow.Year, 4, 1);
    [ObservableProperty] private DateOnly? _periodEnd;
    [ObservableProperty] private WageMethod _method = WageMethod.Hourly;
    [ObservableProperty] private RoundingRule _rounding = RoundingRule.FloorYen;
    [ObservableProperty] private RemainderPolicy _remainder = RemainderPolicy.LargestRemainder;
    [ObservableProperty] private int _fiscalYearStartMonth = 4;
    [ObservableProperty] private int? _fixedDailyYen;

    partial void OnTotalYenChanged(int value)
        => FormattedTotalYen = YenFormatter.Format(value);

    [RelayCommand]
    public async Task SaveFundAsync()
    {
        try
        {
            await setFund.ExecuteAsync(OfficeId, Year, Month, TotalYen, FundNote,
                actor: Environment.UserName, default);
            ErrorMessage = null;
        }
        catch (ArgumentException ex) { ErrorMessage = ex.Message; }
        catch (InvalidOperationException ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    public async Task SaveSettingsAsync()
    {
        try
        {
            await configureSettings.ExecuteAsync(
                OfficeId,
                new DateRange(PeriodStart, PeriodEnd),
                Method, Rounding, Remainder, FiscalYearStartMonth, FixedDailyYen,
                actor: Environment.UserName, default);
            ErrorMessage = null;
        }
        catch (ArgumentException ex) { ErrorMessage = ex.Message; }
        catch (InvalidOperationException ex) { ErrorMessage = ex.Message; }
    }

    public ObservableCollection<OfficeDto> Offices { get; } = new();

    partial void OnSelectedOfficeChanged(OfficeDto? value)
        => OfficeId = value?.Id ?? Guid.Empty;

    /// <summary>View の Loaded から呼ばれる初期化フック。事業所一覧を読み込む。</summary>
    public Task InitializeAsync(CancellationToken ct = default) => LoadOfficesAsync(ct);

    public async Task LoadOfficesAsync(CancellationToken ct = default)
    {
        var list = await listOfficesUseCase.ExecuteAsync(ct);
        Offices.Clear();
        foreach (var o in list) Offices.Add(o);
    }
}
