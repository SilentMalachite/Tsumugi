using System.Collections.ObjectModel;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tsumugi.App.Formatting;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.UseCases.Office;
using Tsumugi.Application.UseCases.Wage;
using Tsumugi.Domain.Enums;

namespace Tsumugi.App.ViewModels;

/// <summary>
/// 工賃計算プレビュー ViewModel。D3 CalculateWagesUseCase を呼び出してプレビュー表示。
/// 確定は行わない（F5 で別タブ）。
/// </summary>
public sealed partial class WageCalculationViewModel(
    CalculateWagesUseCase calculate,
    ListOfficesUseCase listOfficesUseCase) : ViewModelBase
{
    [ObservableProperty] private OfficeDto? _selectedOffice;
    [ObservableProperty] private Guid _officeId;
    [ObservableProperty] private int _year = DateTime.UtcNow.Year;
    [ObservableProperty] private int _month = DateTime.UtcNow.Month;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _summaryLine;
    [ObservableProperty] private bool _hasMismatchWarning;
    [ObservableProperty] private WageMethod _method;

    public ObservableCollection<WagePreviewLineDto> Lines { get; } = new();

    [RelayCommand]
    public async Task LoadPreviewAsync()
    {
        if (OfficeId == Guid.Empty)
        {
            ErrorMessage = "事業所IDが指定されていません。";
            return;
        }
        IsBusy = true;
        try
        {
            var preview = await calculate.ExecuteAsync(OfficeId, Year, Month, default);
            Lines.Clear();
            foreach (var l in preview.Lines) Lines.Add(l);
            Method = preview.Method;
            SummaryLine =
                $"原資 {YenFormatter.Format(preview.TotalFundYen)}　/　配分計 {YenFormatter.Format(preview.TotalAllocatedYen)}　/　対象 {preview.Lines.Count} 名";
            HasMismatchWarning = preview.Method is WageMethod.Hourly or WageMethod.Equal
                && preview.TotalAllocatedYen != preview.TotalFundYen;
            ErrorMessage = null;
        }
        catch (InvalidOperationException ex) { ErrorMessage = ex.Message; }
        catch (ArgumentException ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
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
