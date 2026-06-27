using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.UseCases.Recipient;

namespace Tsumugi.App.ViewModels;

public sealed partial class RecipientListViewModel(ListRecipientsUseCase listUseCase) : ViewModelBase
{
    public ObservableCollection<RecipientDto> Items { get; } = new();

    [ObservableProperty]
    private RecipientDto? _selected;

    [RelayCommand]
    public async Task LoadAsync()
    {
        Items.Clear();
        var list = await listUseCase.ExecuteAsync(default);
        foreach (var r in list) Items.Add(r);
    }
}
