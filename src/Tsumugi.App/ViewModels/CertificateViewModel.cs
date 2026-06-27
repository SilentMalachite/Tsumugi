using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.UseCases.Certificate;

namespace Tsumugi.App.ViewModels;

public sealed partial class CertificateViewModel(
    ListExpiringCertificatesUseCase listExpiring) : ViewModelBase
{
    public ObservableCollection<ExpiringCertificateDto> ExpiringItems { get; } = new();

    [ObservableProperty]
    private int _thresholdDays = 30;

    [ObservableProperty]
    private DateOnly _asOfDate = DateOnly.FromDateTime(DateTime.Today);

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
}
