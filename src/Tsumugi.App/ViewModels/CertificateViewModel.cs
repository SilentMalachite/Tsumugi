using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.UseCases.Certificate;
using Tsumugi.Application.UseCases.Recipient;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.App.ViewModels;

public sealed partial class CertificateViewModel(
    ListExpiringCertificatesUseCase listExpiring,
    RegisterCertificateUseCase registerUseCase,
    ListRecipientsUseCase listRecipients) : ViewModelBase
{
    public ObservableCollection<ExpiringCertificateDto> ExpiringItems { get; } = new();
    public ObservableCollection<RecipientDto> Recipients { get; } = new();

    [ObservableProperty] private int _thresholdDays = 30;
    [ObservableProperty] private DateOnly _asOfDate = DateOnly.FromDateTime(DateTime.Today);

    // 登録フォーム
    [ObservableProperty] private RecipientDto? _selectedRecipient;
    [ObservableProperty] private Guid _recipientId;
    [ObservableProperty] private string _certificateNumber = string.Empty;
    [ObservableProperty] private DateOnly _validityStart = new(2026, 4, 1);
    [ObservableProperty] private DateOnly? _validityEnd;
    [ObservableProperty] private int _supplyDays = 23;
    [ObservableProperty] private int _monthlyCostCap;
    [ObservableProperty] private string _municipality = string.Empty;
    [ObservableProperty] private string? _saveErrorMessage;
    [ObservableProperty] private string? _overlapWarning;
    [ObservableProperty] private bool _isSaved;

    partial void OnSelectedRecipientChanged(RecipientDto? value)
        => RecipientId = value?.Id ?? Guid.Empty;

    /// <summary>View の Loaded から呼ばれる初期化フック。利用者一覧を読み込む。</summary>
    public Task InitializeAsync(CancellationToken ct = default) => LoadRecipientsAsync(ct);

    public async Task LoadRecipientsAsync(CancellationToken ct = default)
    {
        var list = await listRecipients.ExecuteAsync(ct);
        Recipients.Clear();
        foreach (var r in list) Recipients.Add(r);
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        await LoadAsync(AsOfDate, ThresholdDays);
    }

    public async Task LoadAsync(DateOnly asOf, int thresholdDays)
    {
        ExpiringItems.Clear();
        var hits = await listExpiring.ExecuteAsync(asOf, thresholdDays, default);
        foreach (var h in hits) ExpiringItems.Add(h);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            var validity = new DateRange(ValidityStart, ValidityEnd);
            var (_, warnings) = await registerUseCase.ExecuteAsync(
                RecipientId, CertificateNumber, validity,
                SupplyDays, MonthlyCostCap, Municipality,
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
}
