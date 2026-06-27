using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tsumugi.Application;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.UseCases.Recipient;

namespace Tsumugi.App.ViewModels;

public sealed partial class RecipientListViewModel(
    ListRecipientsUseCase listUseCase,
    ArchiveRecipientUseCase archiveUseCase,
    RestoreRecipientUseCase restoreUseCase) : ViewModelBase
{
    public ObservableCollection<RecipientDto> Items { get; } = new();

    [ObservableProperty]
    private RecipientDto? _selected;

    /// <summary>アーカイブ済み利用者も一覧に表示するか。トグル変更で自動再ロード。</summary>
    [ObservableProperty]
    private bool _includeArchived;

    /// <summary>削除/復元の結果メッセージ（成功時は null）。</summary>
    [ObservableProperty]
    private string? _operationMessage;

    /// <summary>
    /// MainWindow が「編集タブへ切替 + RecipientEdit.LoadForEdit」を購読する橋渡しコールバック。
    /// VM 間の直接結合を避けるため Action で受ける。
    /// </summary>
    public Action<RecipientDto>? EditRequested { get; set; }

    [RelayCommand]
    public async Task LoadAsync()
    {
        Items.Clear();
        var list = await listUseCase.ExecuteAsync(IncludeArchived, default);
        foreach (var r in list) Items.Add(r);
    }

    [RelayCommand]
    private void Edit()
    {
        if (Selected is { } dto && !dto.IsArchived) EditRequested?.Invoke(dto);
    }

    /// <summary>選択中の利用者をアーカイブ（論理削除）する。</summary>
    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (Selected is not { } dto || dto.IsArchived) return;
        try
        {
            await archiveUseCase.ExecuteAsync(
                dto.Id, dto.ConcurrencyToken, actor: Environment.UserName, default);
            OperationMessage = null;
            await LoadAsync();
        }
        catch (OptimisticConcurrencyException)
        {
            OperationMessage = "他のユーザに先に更新されています。一覧を再読込してから削除してください。";
        }
        catch (InvalidOperationException ex)
        {
            OperationMessage = ex.Message;
        }
    }

    /// <summary>アーカイブ済みの利用者を復元する。</summary>
    [RelayCommand]
    private async Task RestoreAsync()
    {
        if (Selected is not { } dto || !dto.IsArchived) return;
        try
        {
            await restoreUseCase.ExecuteAsync(
                dto.Id, dto.ConcurrencyToken, actor: Environment.UserName, default);
            OperationMessage = null;
            await LoadAsync();
        }
        catch (OptimisticConcurrencyException)
        {
            OperationMessage = "他のユーザに先に更新されています。一覧を再読込してから復元してください。";
        }
        catch (InvalidOperationException ex)
        {
            OperationMessage = ex.Message;
        }
    }

    partial void OnIncludeArchivedChanged(bool value)
    {
        // チェック変更時に即時再ロード（fire-and-forget）。
        _ = LoadAsync();
    }
}
