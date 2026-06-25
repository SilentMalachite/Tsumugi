using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Tsumugi.Infrastructure.Persistence;

/// <summary>
/// SQLite 保存先のディレクトリ／DBファイルを作成し、OS 別の最小権限で初期化する。
/// Unix: dir 0700 / db 0600。Windows: 現在ユーザーのみフルコントロール（Task 3 で追加）。
/// WAL/SHM サイドカーはディレクトリ権限（0700）で保護される。
/// </summary>
public sealed class SqliteLocationService : ISqliteLocation
{
    private readonly string _directory;

    public SqliteLocationService(string applicationDataRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationDataRoot);
        _directory = applicationDataRoot;
        DatabasePath = Path.Combine(applicationDataRoot, "tsumugi.db");
        ConnectionString = $"Data Source={DatabasePath}";
    }

    public string DatabasePath { get; }
    public string ConnectionString { get; }

    public void EnsureSecuredStorage()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            EnsureUnix();
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            EnsureWindows(); // Task 3 で実装
            return;
        }

        throw new PlatformNotSupportedException(
            "サポートされないOSで Tsumugi の保存先を初期化しようとした。");
    }

    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    private void EnsureUnix()
    {
        const UnixFileMode dirMode =
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
        const UnixFileMode fileMode =
            UnixFileMode.UserRead | UnixFileMode.UserWrite;

        if (!Directory.Exists(_directory))
        {
            Directory.CreateDirectory(_directory, dirMode);
        }
        // 既存ゆるい権限の締め直しは Task 4 で追加

        if (!File.Exists(DatabasePath))
        {
            // 空ファイルを先に作って 0600 を強制 → SQLite は空ファイルを新規DBとして扱う
            using (File.Create(DatabasePath)) { }
            File.SetUnixFileMode(DatabasePath, fileMode);
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private void EnsureWindows()
    {
        // Task 3 で DACL 設定を実装
        throw new NotImplementedException(
            "SqliteLocationService の Windows 分岐は Task 3 で実装する。");
    }
}
