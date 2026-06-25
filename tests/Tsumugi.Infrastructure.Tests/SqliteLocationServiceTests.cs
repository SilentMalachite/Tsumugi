using System;
using System.IO;
using System.Runtime.InteropServices;
using FluentAssertions;
using Tsumugi.Infrastructure.Persistence;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

public sealed class SqliteLocationServiceTests
{
    private static string NewTempDir() =>
        Path.Combine(Path.GetTempPath(), $"tsumugi-loc-{Guid.NewGuid():N}");

    private static bool IsUnix =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
        || RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    [Fact]
    public void EnsureSecuredStorage_creates_dir_0700_and_db_0600_on_unix()
    {
        if (!IsUnix) return; // 非該当OSではクリーンにスキップ

        var dir = NewTempDir();
        try
        {
            var svc = new SqliteLocationService(dir);
            svc.EnsureSecuredStorage();

            Directory.Exists(dir).Should().BeTrue();
            File.Exists(svc.DatabasePath).Should().BeTrue();

#pragma warning disable CA1416 // if (!IsUnix) return; が先行しているため Windows では到達しない
            var dirMode = File.GetUnixFileMode(dir);
            var dbMode = File.GetUnixFileMode(svc.DatabasePath);
#pragma warning restore CA1416

            dirMode.Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                "ディレクトリは所有者のみ rwx（0700）であるべき");
            dbMode.Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite,
                "DBファイルは所有者のみ rw（0600）であるべき");
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }
}
