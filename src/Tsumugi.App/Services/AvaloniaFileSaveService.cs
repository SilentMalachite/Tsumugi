using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using AvaloniaApplication = Avalonia.Application;

namespace Tsumugi.App.Services;

/// <summary>Avalonia の <see cref="IStorageProvider"/> を介する <see cref="IFileSaveService"/> 実装。</summary>
public sealed class AvaloniaFileSaveService : IFileSaveService
{
    public async Task<bool> SaveAsync(
        byte[] bytes,
        string suggestedFileName,
        string fileTypeName,
        string extension,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentNullException.ThrowIfNull(suggestedFileName);
        ArgumentNullException.ThrowIfNull(fileTypeName);
        ArgumentNullException.ThrowIfNull(extension);

        var topLevel = ResolveTopLevel()
            ?? throw new InvalidOperationException("保存ダイアログを開く TopLevel が解決できません。");

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = suggestedFileName,
            FileTypeChoices = new[]
            {
                new FilePickerFileType(fileTypeName) { Patterns = new[] { "*" + extension } },
            },
            DefaultExtension = extension.TrimStart('.'),
        });
        if (file is null) return false;

        var path = file.TryGetLocalPath();
        if (string.IsNullOrEmpty(path))
            throw new InvalidOperationException("保存先パスをローカルファイルとして解決できません。");

        await WriteAtomicallyAsync(path, bytes, ct);
        return true;
    }

    /// <summary>
    /// 同一ディレクトリの一時ファイルへ書き切ってから置換する。保存先へ直接書くと、
    /// 切り詰めた後に取消・I/O 失敗が起きたとき、空または途中までの請求ファイルが残る。
    /// </summary>
    private static async Task WriteAtomicallyAsync(string path, byte[] bytes, CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(path);
        var temporaryPath = Path.Combine(
            string.IsNullOrEmpty(directory) ? "." : directory,
            $".{Path.GetFileName(path)}.{Guid.CreateVersion7():N}.tmp");
        try
        {
            await File.WriteAllBytesAsync(temporaryPath, bytes, ct);
            if (File.Exists(path))
            {
                File.Replace(temporaryPath, path, destinationBackupFileName: null);
            }
            else
            {
                File.Move(temporaryPath, path);
            }
        }
        finally
        {
            // 置換に成功していれば一時ファイルは残らない。失敗・取消時だけ後片付けする。
            if (File.Exists(temporaryPath))
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (IOException)
                {
                    // 後片付けの失敗は保存結果に影響しないため飲み込む。
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }

    private static TopLevel? ResolveTopLevel()
    {
        if (AvaloniaApplication.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            && desktop.MainWindow is { } mw)
        {
            return TopLevel.GetTopLevel(mw);
        }
        return null;
    }
}
