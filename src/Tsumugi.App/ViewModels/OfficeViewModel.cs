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
    ListOfficesUseCase listUseCase,
    UpdateOfficeUseCase updateUseCase) : ViewModelBase
{
    [ObservableProperty] private string _officeNumber = string.Empty;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private ServiceCategory _category = ServiceCategory.TypeB;
    [ObservableProperty] private RegionGrade _region = RegionGrade.None;
    [ObservableProperty] private string? _saveErrorMessage;
    [ObservableProperty] private bool _isSaved;

    // 編集モード: 既存事業所を選ぶとフォームが populate され、UpdateCommand が有効になる。
    [ObservableProperty] private OfficeDto? _selectedItem;
    [ObservableProperty] private Guid? _editingId;

    public ObservableCollection<OfficeDto> Items { get; } = [];

    public async Task LoadAsync(CancellationToken ct = default)
    {
        var list = await listUseCase.ExecuteAsync(ct);
        Items.Clear();
        foreach (var item in list)
            Items.Add(item);
    }

    partial void OnSelectedItemChanged(OfficeDto? value)
    {
        if (value is null)
        {
            EditingId = null;
            return;
        }
        EditingId = value.Id;
        OfficeNumber = value.OfficeNumber;
        Name = value.Name;
        Category = value.ServiceCategory;
        Region = value.RegionGrade;
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

    [RelayCommand]
    private async Task UpdateAsync()
    {
        if (EditingId is null)
        {
            SaveErrorMessage = "編集対象が選択されていません。";
            IsSaved = false;
            return;
        }
        try
        {
            await updateUseCase.ExecuteAsync(EditingId.Value, Name, Category, Region, default);
            SaveErrorMessage = null;
            IsSaved = true;
            await LoadAsync();
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
        OfficeNumber = string.Empty;
        Name = string.Empty;
        Category = ServiceCategory.TypeB;
        Region = RegionGrade.None;
        SaveErrorMessage = null;
        IsSaved = false;
        SelectedItem = null;
    }
}
