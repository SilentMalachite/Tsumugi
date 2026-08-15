using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Tsumugi.Infrastructure.Persistence;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

/// <summary>
/// VACUUM INTO は宛先ファイルが既存だと失敗する（https://www.sqlite.org/lang_vacuum.html §2.1）。
/// 一時名へ書いてから移動することで、同名の既存バックアップがあっても上書きできることを固定する。
/// </summary>
public sealed class SqliteBackupServiceTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "tsumugi-backup-" + Guid.NewGuid().ToString("N"));

    private TsumugiDbContext NewContext()
    {
        Directory.CreateDirectory(_root);
        var options = new DbContextOptionsBuilder<TsumugiDbContext>()
            .UseSqlite($"Data Source={Path.Combine(_root, "tsumugi.db")}")
            .Options;
        var db = new TsumugiDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    [Fact]
    public async Task BackupToAsync_creates_the_destination_file()
    {
        using var db = NewContext();
        var sut = new SqliteBackupService(db);
        var destination = Path.Combine(_root, "out.db");

        await sut.BackupToAsync(destination, CancellationToken.None);

        File.Exists(destination).Should().BeTrue();
        new FileInfo(destination).Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task BackupToAsync_overwrites_an_existing_destination()
    {
        using var db = NewContext();
        var sut = new SqliteBackupService(db);
        var destination = Path.Combine(_root, "out.db");
        await File.WriteAllTextAsync(destination, "既存の中身", CancellationToken.None);

        // VACUUM INTO を宛先へ直接発行していると、ここで SqliteException になる。
        await sut.BackupToAsync(destination, CancellationToken.None);

        var head = await File.ReadAllBytesAsync(destination, CancellationToken.None);
        // SQLite ファイルは "SQLite format 3\0" で始まる
        System.Text.Encoding.ASCII.GetString(head, 0, 15).Should().Be("SQLite format 3");
    }

    [Fact]
    public void EnsureSecuredStorage_creates_the_backup_directory_with_the_same_permissions()
    {
        var location = new SqliteLocationService(_root);
        location.EnsureSecuredStorage();

        Directory.Exists(location.BackupDirectory).Should().BeTrue();

        // 該当 OS 以外は早期 return（xUnit 2.x のため Skip.If は使わない）
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            && !RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return;

        File.GetUnixFileMode(location.BackupDirectory).Should().Be(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    [Fact]
    public async Task BackupToAsync_applies_the_permission_policy_to_the_written_file()
    {
        using var db = NewContext();
        var sut = new SqliteBackupService(db);
        var destination = Path.Combine(_root, "out.db");

        await sut.BackupToAsync(destination, CancellationToken.None);

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            && !RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return;

        File.GetUnixFileMode(destination).Should().Be(
            UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    [Fact]
    public async Task BackupToAsync_leaves_no_temporary_file_behind()
    {
        using var db = NewContext();
        var sut = new SqliteBackupService(db);
        var destination = Path.Combine(_root, "out.db");

        await sut.BackupToAsync(destination, CancellationToken.None);

        Directory.EnumerateFiles(_root, "*.tmp").Should().BeEmpty();
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
