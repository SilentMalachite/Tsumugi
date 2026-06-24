using Microsoft.EntityFrameworkCore;
using Tsumugi.Application.Abstractions;

namespace Tsumugi.Infrastructure.Persistence;

public sealed class SqliteBackupService(TsumugiDbContext db) : IBackupService
{
    public async Task BackupToAsync(string destinationPath, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(destinationPath);

        // SQLite の VACUUM INTO は単一ファイルの一貫したバックアップを生成する。
        // パスはパラメータ化できないため、シングルクォートをエスケープして埋め込む。
        var escaped = destinationPath.Replace("'", "''");
#pragma warning disable EF1002 // VACUUM INTO はパラメータ化不可。シングルクォートをエスケープして埋め込む。
        await db.Database.ExecuteSqlRawAsync($"VACUUM INTO '{escaped}'", ct);
#pragma warning restore EF1002
    }
}
