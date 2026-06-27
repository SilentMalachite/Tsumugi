using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.UseCases.Contract;
using Tsumugi.Application.UseCases.Recipient;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.App.ViewModels;

public sealed partial class ContractViewModel(
    RegisterContractUseCase registerUseCase,
    ListContractsByRecipientUseCase listUseCase,
    ListRecipientsUseCase listRecipientsUseCase) : ViewModelBase
{
    [ObservableProperty] private Guid _recipientId;
    [ObservableProperty] private RecipientDto? _selectedRecipient;
    [ObservableProperty] private DateOnly _periodStart = new(2026, 4, 1);
    [ObservableProperty] private DateOnly? _periodEnd;
    [ObservableProperty] private int _contractedSupplyDays;
    [ObservableProperty] private string? _saveErrorMessage;
    [ObservableProperty] private string? _overlapWarning;
    [ObservableProperty] private bool _isSaved;

    public ObservableCollection<ContractDto> Items { get; } = [];
    public ObservableCollection<RecipientDto> Recipients { get; } = [];

    public async Task LoadRecipientsAsync(CancellationToken ct = default)
    {
        var list = await listRecipientsUseCase.ExecuteAsync(ct);
        Recipients.Clear();
        foreach (var r in list)
            Recipients.Add(r);
    }

    partial void OnSelectedRecipientChanged(RecipientDto? value)
    {
        // 画面上の利用者選択を RecipientId に同期する（未選択時は Guid.Empty に戻す）。
        RecipientId = value?.Id ?? Guid.Empty;
    }

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

    [RelayCommand]
    private void Discard()
    {
        SelectedRecipient = null;  // RecipientId は OnSelectedRecipientChanged 経由で Guid.Empty に戻る
        PeriodStart = default;
        PeriodEnd = null;
        ContractedSupplyDays = 0;
        SaveErrorMessage = null;
        OverlapWarning = null;
        IsSaved = false;
    }
}
