using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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

        await File.WriteAllBytesAsync(path, bytes, ct);
        return true;
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
