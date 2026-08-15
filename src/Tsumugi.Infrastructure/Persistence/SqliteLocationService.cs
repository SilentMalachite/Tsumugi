using System;
using System.IO;
using Tsumugi.Application.Abstractions;

namespace Tsumugi.Infrastructure.Persistence;

/// <summary>
/// SQLite 保存先のディレクトリ／DBファイルを作成し、OS 別の最小権限で初期化する。
/// Unix: dir 0700 / db 0600。Windows: 現在ユーザーのみフルコントロール / 継承無効。
/// WAL/SHM サイドカーはディレクトリ権限（0700 / Windows は親 DACL）で保護される。
/// </summary>
public sealed class SqliteLocationService : ISqliteLocation, IDatabaseFileLocation
{
    private readonly string _directory;

    public SqliteLocationService(string applicationDataRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationDataRoot);
        _directory = applicationDataRoot;
        DatabasePath = Path.Combine(applicationDataRoot, "tsumugi.db");
        BackupDirectory = Path.Combine(applicationDataRoot, "backups");
        ConnectionString = $"Data Source={DatabasePath}";
    }

    public string DatabasePath { get; }
    public string BackupDirectory { get; }
    public string ConnectionString { get; }

    public void EnsureSecuredStorage()
    {
        SecureFileSystem.EnsureDirectory(_directory);
        SecureFileSystem.EnsureFile(DatabasePath);
        SecureFileSystem.EnsureDirectory(BackupDirectory);
    }
}
