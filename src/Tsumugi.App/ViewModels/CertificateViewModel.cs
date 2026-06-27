using System.Collections.ObjectModel;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.UseCases.Certificate;

namespace Tsumugi.App.ViewModels;

public sealed partial class CertificateViewModel(
    ListExpiringCertificatesUseCase listExpiring) : ViewModelBase
{
    public ObservableCollection<ExpiringCertificateDto> ExpiringItems { get; } = new();

    public async Task LoadAsync(DateOnly asOf, int thresholdDays)
    {
        ExpiringItems.Clear();
        var hits = await listExpiring.ExecuteAsync(asOf, thresholdDays, default);
        foreach (var h in hits) ExpiringItems.Add(h);
    }
}
