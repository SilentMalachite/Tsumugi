using System;
using System.IO;
using System.Threading;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Tsumugi.Infrastructure.Persistence;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

public sealed class SqliteRestoreServiceTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "tsumugi-restore-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Restore_brings_back_the_content_of_the_backup()
    {
        var location = new SqliteLocationService(_root);
        location.EnsureSecuredStorage();

        var options = new DbContextOptionsBuilder<TsumugiDbContext>()
            .UseSqlite(location.ConnectionString).Options;

        var backupPath = Path.Combine(location.BackupDirectory, "snapshot.db");

        // 1. スキーマを作り、バックアップを取る
        using (var db = new TsumugiDbContext(options))
        {
            db.Database.EnsureCreated();
            await new SqliteBackupService(db).BackupToAsync(backupPath, CancellationToken.None);
        }
        SqliteConnection.ClearAllPools();

        // 2. 現行 DB を壊す（中身を潰す）
        await File.WriteAllTextAsync(location.DatabasePath, "壊れたDB", CancellationToken.None);

        // 3. 復元する
        await new SqliteRestoreService(location).RestoreFromAsync(backupPath, CancellationToken.None);

        // 4. 復元後の DB が SQLite として開けること
        using var restored = new TsumugiDbContext(options);
        var act = () => restored.Database.CanConnect();
        act.Should().NotThrow();
        restored.Database.CanConnect().Should().BeTrue();
    }

    [Fact]
    public async Task Restore_deletes_stale_wal_and_shm_sidecars()
    {
        var location = new SqliteLocationService(_root);
        location.EnsureSecuredStorage();

        var options = new DbContextOptionsBuilder<TsumugiDbContext>()
            .UseSqlite(location.ConnectionString).Options;
        var backupPath = Path.Combine(location.BackupDirectory, "snapshot.db");

        using (var db = new TsumugiDbContext(options))
        {
            db.Database.EnsureCreated();
            await new SqliteBackupService(db).BackupToAsync(backupPath, CancellationToken.None);
        }
        SqliteConnection.ClearAllPools();

        await File.WriteAllTextAsync(location.DatabasePath + "-wal", "stale", CancellationToken.None);
        await File.WriteAllTextAsync(location.DatabasePath + "-shm", "stale", CancellationToken.None);

        await new SqliteRestoreService(location).RestoreFromAsync(backupPath, CancellationToken.None);

        File.Exists(location.DatabasePath + "-wal").Should().BeFalse();
        File.Exists(location.DatabasePath + "-shm").Should().BeFalse();
    }

    [Fact]
    public async Task Restore_throws_when_the_source_is_missing_and_the_message_has_no_path()
    {
        var location = new SqliteLocationService(_root);
        location.EnsureSecuredStorage();
        var sut = new SqliteRestoreService(location);

        var act = async () => await sut.RestoreFromAsync(
            Path.Combine(_root, "does-not-exist.db"), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<FileNotFoundException>();
        ex.Which.Message.Should().NotContain(_root);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
