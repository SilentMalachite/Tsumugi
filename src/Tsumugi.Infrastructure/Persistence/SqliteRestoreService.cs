using Microsoft.Data.Sqlite;
using Tsumugi.Application.Abstractions;

namespace Tsumugi.Infrastructure.Persistence;

/// <summary>
/// DB ファイルの置換。接続プールを解放してから差し替え、WAL/SHM サイドカーを削除する。
/// 古い WAL が新しい DB に適用されると破損するため、サイドカーの削除は必須。
/// </summary>
public sealed class SqliteRestoreService(ISqliteLocation location) : IDatabaseRestoreService
{
    public Task RestoreFromAsync(string backupFilePath, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupFilePath);
        if (!File.Exists(backupFilePath))
        {
            throw new FileNotFoundException("復元元のバックアップファイルが見つかりません。");
        }

        ct.ThrowIfCancellationRequested();

        // Microsoft.Data.Sqlite は接続をプールするため、解放しないとファイルを掴んだままになる。
        SqliteConnection.ClearAllPools();

        // 一時名へコピーしてから移動する。コピーが途中で失敗しても現行 DB は無傷のまま残る
        // （SqliteBackupService と同じ方針。破壊的な置換ほど原子的であるべき）。
        var stagingPath = location.DatabasePath + ".restore-tmp";
        if (File.Exists(stagingPath)) File.Delete(stagingPath);

        try
        {
            File.Copy(backupFilePath, stagingPath, overwrite: true);

            // 古い WAL/SHM は「新しい DB が現れる前」に消す。後に回すと、新しい中身と
            // 古いサイドカーが同居する窓が開き、古い WAL が適用されて破損しうる。
            foreach (var sidecar in new[] { location.DatabasePath + "-wal", location.DatabasePath + "-shm" })
            {
                if (File.Exists(sidecar)) File.Delete(sidecar);
            }

            File.Move(stagingPath, location.DatabasePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(stagingPath)) File.Delete(stagingPath);
        }

        SecureFileSystem.EnsureFile(location.DatabasePath);
        return Task.CompletedTask;
    }
}
