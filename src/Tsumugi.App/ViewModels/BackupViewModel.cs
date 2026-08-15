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
    IFileSaveService fileSave,
    IApplicationShutdown shutdown) : ViewModelBase
{
    [ObservableProperty] private string? _selectedGeneration;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _restartRequired;

    /// <summary>
    /// 復元の2段階確認（arm → confirm）の1段目が済んでいるかどうか。
    /// 稼働中の DB を置き換える破壊的操作のため、キー1発で実行させない（レビュー指摘1）。
    /// </summary>
    [ObservableProperty] private bool _restoreArmed;

    /// <summary>
    /// 「今すぐバックアップ」「控えを保存」「選択した世代へ復元」のいずれかが実行中かどうか。
    /// <see cref="IBackupService"/> は scoped で同一 <c>TsumugiDbContext</c> を共有するため、
    /// 3操作が重なると EF Core が例外を投げる。窓を狭めるためボタンを無効化する
    /// （最終レビュー指摘5。終了時フックは VM を経由しないため完全には塞がらない）。
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BackupNowCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveCopyCommand))]
    [NotifyCanExecuteChangedFor(nameof(RestoreCommand))]
    private bool _isBusy;

    public ObservableCollection<string> Generations { get; } = new();

    partial void OnSelectedGenerationChanged(string? value) => RestoreArmed = false;

    private bool CanRunBackupOperation() => !IsBusy;

    [RelayCommand]
    public Task LoadAsync()
    {
        Generations.Clear();
        foreach (var name in listGenerations.Execute()) Generations.Add(name);
        return Task.CompletedTask;
    }

    [RelayCommand(CanExecute = nameof(CanRunBackupOperation))]
    public async Task BackupNowAsync()
    {
        if (IsBusy) return;
        ErrorMessage = null;
        StatusMessage = null;
        IsBusy = true;
        try
        {
            await runBackup.ExecuteAsync(CancellationToken.None);
            await LoadAsync();
            StatusMessage = "バックアップを作成しました。";
        }
        catch (Exception)
        {
            // 例外メッセージには生のファイル I/O 由来のフルパスが載りうるため、画面へ出さない
            // （CLAUDE.md ハード制約4）。本アプリはログ機構を持たないので詳細は保持しない。
            ErrorMessage = "バックアップに失敗しました。保存先の空き容量とアクセス権を確認してください。";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunBackupOperation))]
    public async Task SaveCopyAsync()
    {
        if (IsBusy) return;
        ErrorMessage = null;
        StatusMessage = null;
        IsBusy = true;
        try
        {
            var (suggestedFileName, content) = await exportCopy.ExecuteAsync(CancellationToken.None);
            var saved = await fileSave.SaveAsync(
                content, suggestedFileName, "SQLite データベース", ".db", CancellationToken.None);
            StatusMessage = saved ? "控えを保存しました。" : null;
        }
        catch (Exception)
        {
            // 例外メッセージには生のファイル I/O 由来のフルパスが載りうるため、画面へ出さない
            // （CLAUDE.md ハード制約4）。本アプリはログ機構を持たないので詳細は保持しない。
            ErrorMessage = "控えの保存に失敗しました。保存先の空き容量とアクセス権を確認してください。";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// 選択した世代への復元。稼働中の DB を置き換える破壊的操作のため、
    /// 1回目の呼び出しは実行せず確認の関門（<see cref="RestoreArmed"/>）を立てるだけにし、
    /// 2回目の呼び出しで実際に復元する。<see cref="SelectedGeneration"/> が変わると関門は下がる。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRunBackupOperation))]
    public async Task RestoreAsync()
    {
        if (IsBusy) return;
        ErrorMessage = null;
        StatusMessage = null;
        if (string.IsNullOrWhiteSpace(SelectedGeneration)) return;

        if (!RestoreArmed)
        {
            RestoreArmed = true;
            StatusMessage = "復元すると現在のデータベースは置き換わります。もう一度「復元」を押すと実行します。";
            return;
        }

        IsBusy = true;
        try
        {
            // 引数はバックアップディレクトリ直下のファイル名。VM は保存先を知らない。
            await restore.ExecuteAsync(SelectedGeneration, actor: "operator", CancellationToken.None);
            RestartRequired = true;
            StatusMessage = "復元しました。アプリを終了します。再起動してください。";

            // 稼働中の DbContext の下で DB ファイルを差し替えたため、そのまま使い続けると
            // 接続・ChangeTracker が古い DB を指したまま残る（ADR 0052 決定6。運用上の推奨
            // ではなく要件）。復元が成功したときだけ終了する。arm 段階・失敗時は呼ばない。
            shutdown.RequestShutdown();
        }
        catch (Exception)
        {
            // 例外メッセージには生のファイル I/O 由来のフルパスが載りうるため、画面へ出さない
            // （CLAUDE.md ハード制約4）。本アプリはログ機構を持たないので詳細は保持しない。
            ErrorMessage = "復元に失敗しました。バックアップファイルの状態と保存先のアクセス権を確認してください。";
        }
        finally
        {
            RestoreArmed = false;
            IsBusy = false;
        }
    }
}
