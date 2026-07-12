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
    [ObservableProperty] private string _postalCode = string.Empty;
    [ObservableProperty] private string _address = string.Empty;
    [ObservableProperty] private string _phoneNumber = string.Empty;
    [ObservableProperty] private string _representativeTitleAndName = string.Empty;
    [ObservableProperty] private string? _saveErrorMessage;
    [ObservableProperty] private bool _isSaved;

    // 編集モード: 既存事業所を選ぶとフォームが populate され、UpdateCommand が有効になる。
    [ObservableProperty] private OfficeDto? _selectedItem;
    [ObservableProperty] private Guid? _editingId;
    // 楽観的同時実行: SelectedItem 時点のトークンを保持し、Update でそのまま渡す。
    private Guid _editingConcurrencyToken;

    public ObservableCollection<OfficeDto> Items { get; } = [];

    /// <summary>ナビゲーション由来の事業所だけを読み込み、選択する。</summary>
    public async Task<bool> ApplyNavigationContextAsync(
        Guid? officeId,
        CancellationToken ct = default)
    {
        if (officeId is not { } id)
            return true;

        await LoadAsync(ct);
        SelectedItem = Items.SingleOrDefault(item => item.Id == id);
        return SelectedItem is not null;
    }

    [RelayCommand]
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
            _editingConcurrencyToken = Guid.Empty;
            return;
        }
        EditingId = value.Id;
        _editingConcurrencyToken = value.ConcurrencyToken;
        OfficeNumber = value.OfficeNumber;
        Name = value.Name;
        Category = value.ServiceCategory;
        Region = value.RegionGrade;
        PostalCode = value.PostalCode ?? string.Empty;
        Address = value.Address ?? string.Empty;
        PhoneNumber = value.PhoneNumber ?? string.Empty;
        RepresentativeTitleAndName = value.RepresentativeTitleAndName ?? string.Empty;
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
            await updateUseCase.ExecuteAsync(
                EditingId.Value, _editingConcurrencyToken,
                Name, Category, Region,
                NullIfEmpty(PostalCode), NullIfEmpty(Address), NullIfEmpty(PhoneNumber),
                NullIfEmpty(RepresentativeTitleAndName),
                Environment.UserName, default);
            SaveErrorMessage = null;
            IsSaved = true;
            await LoadAsync();
        }
        catch (ArgumentException ex)
        {
            SaveErrorMessage = ex.Message;
            IsSaved = false;
        }
        catch (Tsumugi.Application.OptimisticConcurrencyException)
        {
            SaveErrorMessage = "他のユーザに先に更新されています。一覧を再選択して最新状態を読み込んでください。";
            IsSaved = false;
        }
    }

    [RelayCommand]
    private async Task SaveCurrentAsync()
    {
        if (EditingId is null)
            await SaveAsync();
        else
            await UpdateAsync();
    }

    [RelayCommand]
    private void Discard()
    {
        OfficeNumber = string.Empty;
        Name = string.Empty;
        Category = ServiceCategory.TypeB;
        Region = RegionGrade.None;
        PostalCode = string.Empty;
        Address = string.Empty;
        PhoneNumber = string.Empty;
        RepresentativeTitleAndName = string.Empty;
        SaveErrorMessage = null;
        IsSaved = false;
        SelectedItem = null;
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
