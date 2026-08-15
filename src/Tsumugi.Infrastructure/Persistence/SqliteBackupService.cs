using Microsoft.EntityFrameworkCore;
using Tsumugi.Application.Abstractions;

namespace Tsumugi.Infrastructure.Persistence;

public sealed class SqliteBackupService(TsumugiDbContext db) : IBackupService
{
    public async Task BackupToAsync(string destinationPath, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        // VACUUM INTO は宛先が既存だと失敗する（空ファイルなら可）。
        // https://www.sqlite.org/lang_vacuum.html §2.1
        // そのため一時名へ書き、成功してから移動する。移動は同一ディレクトリ内なので原子的。
        var temporaryPath = destinationPath + ".tmp";
        if (File.Exists(temporaryPath)) File.Delete(temporaryPath);

        var escaped = temporaryPath.Replace("'", "''");
        try
        {
            // SQLite の VACUUM INTO は単一ファイルの一貫したバックアップを生成する。
            // パスはパラメータ化できないため、シングルクォートをエスケープして埋め込む。
#pragma warning disable EF1002 // VACUUM INTO はパラメータ化不可。シングルクォートをエスケープして埋め込む。
            await db.Database.ExecuteSqlRawAsync($"VACUUM INTO '{escaped}'", ct);
#pragma warning restore EF1002

            File.Move(temporaryPath, destinationPath, overwrite: true);
        }
        finally
        {
            // 失敗時に半端な一時ファイルを残さない。
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }

        // 保存先の権限を締める。外部媒体で適用できない場合は警告扱いで続行する。
        SecureFileSystem.TryEnsureFile(destinationPath);
    }
}
