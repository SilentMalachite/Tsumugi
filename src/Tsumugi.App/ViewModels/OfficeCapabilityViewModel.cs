using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.UseCases.Office;
using Tsumugi.Application.UseCases.OfficeCapability;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.App.ViewModels;

/// <summary>
/// 事業所体制（食事提供 / 送迎支援 等）の登録画面 ViewModel。
/// 体制フラグキーは docs/open-questions.md 通り Phase 1 では暫定（mealProvision / transportSupport）。
/// </summary>
public sealed partial class OfficeCapabilityViewModel(
    RegisterOfficeCapabilityUseCase registerUseCase,
    ListOfficesUseCase listOfficesUseCase) : ViewModelBase
{
    [ObservableProperty] private OfficeDto? _selectedOffice;
    [ObservableProperty] private Guid _officeId;
    [ObservableProperty] private DateOnly _periodStart = new(2026, 4, 1);
    [ObservableProperty] private DateOnly? _periodEnd;
    [ObservableProperty] private bool _mealProvision;
    [ObservableProperty] private bool _transportSupport;
    [ObservableProperty] private string? _saveErrorMessage;
    [ObservableProperty] private string? _overlapWarning;
    [ObservableProperty] private bool _isSaved;

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

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            var flags = new Dictionary<string, bool>
            {
                ["mealProvision"] = MealProvision,
                ["transportSupport"] = TransportSupport,
            };
            var (_, warnings) = await registerUseCase.ExecuteAsync(
                OfficeId, new DateRange(PeriodStart, PeriodEnd), flags,
                actor: Environment.UserName, default);
            SaveErrorMessage = null;
            OverlapWarning = warnings.Count > 0 ? string.Join(" ", warnings) : null;
            IsSaved = true;
        }
        catch (ArgumentException ex)
        {
            SaveErrorMessage = ex.Message;
            IsSaved = false;
        }
    }

    [RelayCommand]
    private void Discard()
    {
        SelectedOffice = null;
        PeriodStart = default;
        PeriodEnd = null;
        MealProvision = false;
        TransportSupport = false;
        SaveErrorMessage = null;
        OverlapWarning = null;
        IsSaved = false;
    }
}
