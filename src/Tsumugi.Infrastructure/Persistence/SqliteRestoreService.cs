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

        File.Copy(backupFilePath, location.DatabasePath, overwrite: true);

        foreach (var sidecar in new[] { location.DatabasePath + "-wal", location.DatabasePath + "-shm" })
        {
            if (File.Exists(sidecar)) File.Delete(sidecar);
        }

        SecureFileSystem.EnsureFile(location.DatabasePath);
        return Task.CompletedTask;
    }
}
