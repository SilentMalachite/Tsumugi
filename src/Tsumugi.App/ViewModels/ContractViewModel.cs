using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.UseCases.Contract;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.App.ViewModels;

public sealed partial class ContractViewModel(
    RegisterContractUseCase registerUseCase,
    ListContractsByRecipientUseCase listUseCase) : ViewModelBase
{
    [ObservableProperty] private Guid _recipientId;
    [ObservableProperty] private DateOnly _periodStart = new(2026, 4, 1);
    [ObservableProperty] private DateOnly? _periodEnd;
    [ObservableProperty] private int _contractedSupplyDays;
    [ObservableProperty] private string? _saveErrorMessage;
    [ObservableProperty] private string? _overlapWarning;
    [ObservableProperty] private bool _isSaved;

    public ObservableCollection<ContractDto> Items { get; } = [];

    public async Task LoadAsync(CancellationToken ct = default)
    {
        var list = await listUseCase.ExecuteAsync(RecipientId, ct);
        Items.Clear();
        foreach (var item in list)
            Items.Add(item);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            var period = new DateRange(PeriodStart, PeriodEnd);
            var (_, warnings) = await registerUseCase.ExecuteAsync(
                RecipientId, period, ContractedSupplyDays,
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
