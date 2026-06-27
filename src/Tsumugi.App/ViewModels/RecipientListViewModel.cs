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

    /// <summary>
    /// MainWindow が「編集タブへ切替 + RecipientEdit.LoadForEdit」を購読する橋渡しコールバック。
    /// VM 間の直接結合を避けるため Action で受ける。
    /// </summary>
    public Action<RecipientDto>? EditRequested { get; set; }

    [RelayCommand]
    public async Task LoadAsync()
    {
        Items.Clear();
        var list = await listUseCase.ExecuteAsync(default);
        foreach (var r in list) Items.Add(r);
    }

    [RelayCommand]
    private void Edit()
    {
        if (Selected is { } dto) EditRequested?.Invoke(dto);
    }
}
