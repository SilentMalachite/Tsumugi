using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.UseCases;
using Tsumugi.Application.UseCases.Office;
using Tsumugi.Domain.Enums;

namespace Tsumugi.App.ViewModels;

public sealed partial class OfficeViewModel(
    RegisterOfficeUseCase registerUseCase,
    ListOfficesUseCase listUseCase) : ViewModelBase
{
    [ObservableProperty] private string _officeNumber = string.Empty;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private ServiceCategory _category = ServiceCategory.TypeB;
    [ObservableProperty] private RegionGrade _region = RegionGrade.None;
    [ObservableProperty] private string? _saveErrorMessage;
    [ObservableProperty] private bool _isSaved;

    public ObservableCollection<OfficeDto> Items { get; } = [];

    public async Task LoadAsync(CancellationToken ct = default)
    {
        var list = await listUseCase.ExecuteAsync(ct);
        Items.Clear();
        foreach (var item in list)
            Items.Add(item);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            await registerUseCase.ExecuteAsync(
                OfficeNumber, Name, Category, Region,
                actor: Environment.UserName, default);
            SaveErrorMessage = null;
            IsSaved = true;
        }
        catch (ArgumentException ex)
        {
            SaveErrorMessage = ex.Message;
            IsSaved = false;
        }
    }
}
