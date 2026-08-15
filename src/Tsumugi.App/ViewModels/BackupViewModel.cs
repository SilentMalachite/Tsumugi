using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tsumugi.App.Services;
using Tsumugi.Application.UseCases.Backup;

namespace Tsumugi.App.ViewModels;

/// <summary>
/// バックアップと復元の画面。設定は持たない（spec 決定3）。
/// 保存先と保持世代は固定で、利用者が変更できるのは「控えの保存先」だけ。
/// </summary>
public sealed partial class BackupViewModel(
    RunScheduledBackupUseCase runBackup,
    ListBackupGenerationsUseCase listGenerations,
    RestoreDatabaseUseCase restore,
    ExportBackupCopyUseCase exportCopy,
    IFileSaveService fileSave) : ViewModelBase
{
    [ObservableProperty] private string? _selectedGeneration;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _restartRequired;

    public ObservableCollection<string> Generations { get; } = new();

    [RelayCommand]
    public Task LoadAsync()
    {
        Generations.Clear();
        foreach (var name in listGenerations.Execute()) Generations.Add(name);
        return Task.CompletedTask;
    }

    [RelayCommand]
    public async Task BackupNowAsync()
    {
        ErrorMessage = null;
        try
        {
            await runBackup.ExecuteAsync(CancellationToken.None);
            await LoadAsync();
            StatusMessage = "バックアップを作成しました。";
        }
        catch (Exception ex)
        {
            // 例外メッセージにパスが載らないことは各サービス側で保証している（ハード制約4）。
            ErrorMessage = "バックアップに失敗しました: " + ex.Message;
        }
    }

    [RelayCommand]
    public async Task SaveCopyAsync()
    {
        ErrorMessage = null;
        try
        {
            var (suggestedFileName, content) = await exportCopy.ExecuteAsync(CancellationToken.None);
            var saved = await fileSave.SaveAsync(
                content, suggestedFileName, "SQLite データベース", ".db", CancellationToken.None);
            StatusMessage = saved ? "控えを保存しました。" : null;
        }
        catch (Exception ex)
        {
            ErrorMessage = "控えの保存に失敗しました: " + ex.Message;
        }
    }

    [RelayCommand]
    public async Task RestoreAsync()
    {
        ErrorMessage = null;
        if (string.IsNullOrWhiteSpace(SelectedGeneration)) return;

        try
        {
            // 引数はバックアップディレクトリ直下のファイル名。VM は保存先を知らない。
            await restore.ExecuteAsync(SelectedGeneration, actor: "operator", CancellationToken.None);
            RestartRequired = true;
            StatusMessage = "復元しました。反映するにはアプリを再起動してください。";
        }
        catch (Exception ex)
        {
            ErrorMessage = "復元に失敗しました: " + ex.Message;
        }
    }
}
