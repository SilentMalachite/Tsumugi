using Tsumugi.Application.Abstractions;

namespace Tsumugi.Application.UseCases.Backup;

/// <summary>復元 UI 用に、自動バックアップの世代を新しい順で返す。退避（pre-restore）も含める。</summary>
public sealed class ListBackupGenerationsUseCase(IBackupDirectory backupDirectory)
{
    public IReadOnlyList<string> Execute() =>
        backupDirectory.ListFileNames()
            .Where(n => n.EndsWith(Application.Backup.BackupFileName.Extension, StringComparison.Ordinal))
            .OrderByDescending(n => n, StringComparer.Ordinal)
            .ToArray();
}
