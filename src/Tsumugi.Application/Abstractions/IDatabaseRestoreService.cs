namespace Tsumugi.Application.Abstractions;

/// <summary>
/// バックアップファイルで現行 DB を置き換える。接続プールの解放と WAL/SHM の後始末を含む。
/// 呼び出し後、アプリは再起動される前提（稼働中の DbContext は古い DB を指したままになる）。
/// </summary>
public interface IDatabaseRestoreService
{
    Task RestoreFromAsync(string backupFilePath, CancellationToken ct);
}
