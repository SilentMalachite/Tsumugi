using Tsumugi.Application.Abstractions;

namespace Tsumugi.Infrastructure.Persistence;

/// <summary>バックアップディレクトリ直下の列挙と削除。</summary>
public sealed class BackupDirectoryService(ISqliteLocation location) : IBackupDirectory
{
    public IReadOnlyList<string> ListFileNames()
    {
        if (!Directory.Exists(location.BackupDirectory)) return [];
        return Directory.EnumerateFiles(location.BackupDirectory)
            .Select(Path.GetFileName)
            .Where(n => !string.IsNullOrEmpty(n))
            .Select(n => n!)
            .ToArray();
    }

    public void Delete(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        // ディレクトリ直下に限定する（パス区切りを含む入力を拒否）。
        if (fileName != Path.GetFileName(fileName))
        {
            throw new ArgumentException("バックアップディレクトリ直下のファイル名のみ指定できます。", nameof(fileName));
        }

        var path = Path.Combine(location.BackupDirectory, fileName);
        if (File.Exists(path)) File.Delete(path);
    }
}
